/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,js,svelte,ts}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#fff7ed',
          100: '#ffedd5',
          200: '#fed7aa',
          300: '#fdba74',
          400: '#fb923c',
          500: '#f97316',
          600: '#ea580c',
          700: '#c2410c',
          800: '#9a3412',
          900: '#7c2d12',
          950: '#431407'
        },
        ink: {
          50: '#f5f7fa',
          100: '#e7ebf1',
          200: '#d0d7e2',
          300: '#aeb9cb',
          400: '#8594ad',
          500: '#66748d',
          600: '#515d73',
          700: '#424c5d',
          800: '#303846',
          900: '#252c37',
          950: '#1d2430'
        },
        concrete: '#8B8680',
        framing: '#C4A265',
        'job-green': '#22c55e',
        'warn-orange': '#f59e0b',
        'danger-red': '#ef4444'
      },
      fontFamily: {
        sans: ['Avenir Next', 'Avenir', 'Segoe UI', 'system-ui', 'sans-serif']
      }
    }
  },
  plugins: []
};
