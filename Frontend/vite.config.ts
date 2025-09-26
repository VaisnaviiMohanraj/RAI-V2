import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Production configuration for Vite
export default defineConfig({
  plugins: [react()],
  server: {
    port: 3001,
    host: true, // Allow external connections
    proxy: {
      '/api': {
        target: 'https://site-net-rrai-blue-fsgabaardkdhhnhf.centralus-01.azurewebsites.net', // Production Backend API endpoint
        changeOrigin: true,
        secure: true, // Use HTTPS for production
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
    minify: 'terser',
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          msal: ['@azure/msal-browser', '@azure/msal-react'],
        },
      },
    },
  },
  define: {
    'process.env.NODE_ENV': '"production"',
    'import.meta.env.VITE_FORCE_PRODUCTION_URL': '"true"',
    'import.meta.env.NODE_ENV': '"production"',
  },
})
