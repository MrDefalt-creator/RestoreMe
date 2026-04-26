import type { Config } from 'tailwindcss'

const config: Config = {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        ink: {
          950: '#102033',
          900: '#17314e',
          800: '#26496b',
        },
        surface: {
          50: '#f8fbfd',
          100: '#f0f6fa',
          200: '#dde8ef',
          300: '#c7d5e0',
        },
        accent: {
          500: '#0f8acb',
          600: '#0a6ca3',
        },
        success: {
          500: '#15803d',
        },
        warning: {
          500: '#b7791f',
        },
        danger: {
          500: '#c2410c',
        },
      },
      fontFamily: {
        sans: ['Inter Variable', 'Inter', 'Segoe UI', 'sans-serif'],
        mono: ['JetBrains Mono', 'Fira Code', 'monospace'],
      },
      boxShadow: {
        panel: '0 18px 50px rgba(30, 51, 84, 0.08)',
      },
    },
  },
  plugins: [],
}

export default config
