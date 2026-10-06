// Rendering fixture never submits customer data. Integration behavior stays in release-gates.
export function enhance(node: HTMLFormElement) { const stop = (event: Event) => event.preventDefault(); node.addEventListener('submit', stop); return { destroy() { node.removeEventListener('submit', stop); } }; }
