import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest
import zipfile

WORKFLOW = Path(__file__).resolve().parents[2] / '.github/workflows/deploy.yml'

def step_script(name):
    section = WORKFLOW.read_text().split(f'      - name: {name}\n', 1)[1].split('\n      - name:', 1)[0]
    return textwrap.dedent(section.split('        run: |\n', 1)[1])

class BundleTests(unittest.TestCase):
    def test_each_component_round_trips_without_other_package(self):
        for component in ['api', 'web', 'all']:
            with self.subTest(component=component), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                for name, entry in [('api', 'TurnKeyOps.API.dll'), ('web', 'build/index.js')]:
                    if component not in (name, 'all'): continue
                    with zipfile.ZipFile(root/f'turnkeyops-{name}.zip', 'w') as archive:
                        archive.writestr(entry, 'test artifact')
                env = os.environ | {'DEPLOY_COMPONENT': component, 'RUNNER_TEMP': directory,
                    'GITHUB_SHA': 'test-commit', 'GITHUB_RUN_ID': '42', 'GITHUB_RUN_ATTEMPT': '1', 'PACKAGED_RUN_ATTEMPT': '1'}
                def run(name):
                    return subprocess.run(['bash', '-c', step_script(name)], cwd=root, env=env,
                                          capture_output=True, text=True)
                result = run('Write artifact manifest')
                self.assertEqual(result.returncode, 0, result.stderr)
                result = run('Verify immutable release bundle')
                self.assertEqual(result.returncode, 0, result.stderr)
                # Deployment-only retries use the original packaging attempt.
                env['GITHUB_RUN_ATTEMPT'] = '2'
                self.assertEqual(run('Verify immutable release bundle').returncode, 0)
                env['PACKAGED_RUN_ATTEMPT'] = '2'
                self.assertNotEqual(run('Verify immutable release bundle').returncode, 0)
                env['PACKAGED_RUN_ATTEMPT'] = '1'
                # A package from another component/run may not be substituted.
                env['DEPLOY_COMPONENT'] = 'web' if component == 'api' else 'api'
                self.assertNotEqual(run('Verify immutable release bundle').returncode, 0)
                env['DEPLOY_COMPONENT'] = component
                env['GITHUB_RUN_ID'] = 'different-run'
                self.assertNotEqual(run('Verify immutable release bundle').returncode, 0)
                env['GITHUB_RUN_ID'] = '42'
                next(root.glob('*.zip')).write_bytes(b'tampered')
                self.assertNotEqual(run('Verify immutable release bundle').returncode, 0)

if __name__ == '__main__': unittest.main()
