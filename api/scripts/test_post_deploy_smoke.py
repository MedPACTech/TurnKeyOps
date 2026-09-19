import os
from pathlib import Path
import subprocess
import tempfile
import time
import unittest

SCRIPT = Path(__file__).with_name('post-deploy-smoke.sh')

class SmokeTests(unittest.TestCase):
    def run_smoke(self, component='all', mode='healthy', budget='10'):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            curl = root / 'curl'
            curl.write_text('''#!/usr/bin/env python3
import os, sys, time
args=sys.argv[1:]
with open(os.environ['PROBE_LOG'], 'a') as out: out.write(args[-1]+'\\n')
if os.environ['PROBE_MODE']=='timeout':
 time.sleep(float(args[args.index('--max-time')+1]))
 print('000',end='');sys.exit(28)
if '--head' in args:
 print('HTTP/1.1 303 See Other\\nLocation: /auth/login?returnTo=protected\\n')
else:
 print('401' if args[-1].endswith('/api/quote-requests') else '200',end='')
''')
            curl.chmod(0o755)
            env = os.environ | {'PATH': str(root)+os.pathsep+os.environ['PATH'],
                'PROBE_LOG': str(root/'calls'), 'PROBE_MODE': mode,
                'SMOKE_COMPONENT': component, 'SMOKE_TIMEOUT_SECONDS': budget,
                'SMOKE_API_BASE_URL': 'https://api.invalid',
                'SMOKE_WEB_BASE_URL': 'https://web.invalid'}
            started = time.monotonic()
            result = subprocess.run(['bash', str(SCRIPT)], env=env, capture_output=True, text=True, timeout=10)
            elapsed = time.monotonic()-started
            calls = (root/'calls').read_text().splitlines() if (root/'calls').exists() else []
            return result, calls, elapsed

    def test_api_only_never_probes_web(self):
        result, calls, _ = self.run_smoke('api')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(calls, ['https://api.invalid/', 'https://api.invalid/api/quote-requests'])

    def test_web_checks_api_dependency_and_all_web_surfaces(self):
        result, calls, _ = self.run_smoke('web')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(calls), 7)
        self.assertIn('https://web.invalid/thinkpink/admin/dashboard', calls)

    def test_default_checks_both(self):
        result, calls, _ = self.run_smoke()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(calls), 7)

    def test_timeout_budget_includes_slow_requests(self):
        result, calls, elapsed = self.run_smoke('api', 'timeout', '2')
        self.assertNotEqual(result.returncode, 0)
        self.assertLess(elapsed, 5)
        self.assertEqual(len(calls), 1)

    def test_invalid_component_stops_before_network(self):
        result, calls, _ = self.run_smoke('typo')
        self.assertEqual(result.returncode, 2)
        self.assertEqual(calls, [])

if __name__ == '__main__': unittest.main()
