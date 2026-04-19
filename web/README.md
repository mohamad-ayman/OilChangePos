# OilChangePOS `web/` (React)

This folder is the **browser UI** for OilChangePOS. It is **not** part of `OilChangePOS.sln` (npm/Vite only; the solution holds the .NET API and libraries).

## Step 1 — Foundation (current)

- **Stack:** Vite + React + TypeScript, React Router (data router), TanStack Query, Axios, Zustand, Tailwind CSS v4 (`@tailwindcss/vite`).
- **Imports:** `@/*` → `src/*` (see `vite.config.ts`, `tsconfig.app.json`).
- **Layout:** `src/app/` (entry, providers, router, layout, styles), `src/pages/`, `src/shared/` (`config/`, `api/client.ts`, `store/`, `ui/`, `lib/`), `src/features/`, `src/entities/` (placeholders where needed).

## Run with the API

1. Start **OilChangePOS.API** (Swagger shows `https://localhost:7099` in Development).
2. From this directory: `npm install` then `npm run dev`.
3. Open the URL Vite prints (usually `http://localhost:5173`). Requests to `/api/...` are proxied to the API (see `vite.config.ts`).

For a **production** build on another host, set `VITE_API_BASE_URL` to your API’s public origin (copy `.env.example` to `.env.production` and adjust).

---

# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type-aware lint rules:

```js
export default tseslint.config({
  extends: [
    // Remove ...tseslint.configs.recommended and replace with this
    ...tseslint.configs.recommendedTypeChecked,
    // Alternatively, use this for stricter rules
    ...tseslint.configs.strictTypeChecked,
    // Optionally, add this for stylistic rules
    ...tseslint.configs.stylisticTypeChecked,
  ],
  languageOptions: {
    // other options...
    parserOptions: {
      project: ['./tsconfig.node.json', './tsconfig.app.json'],
      tsconfigRootDir: import.meta.dirname,
    },
  },
})
```

You can also install [eslint-plugin-react-x](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-x) and [eslint-plugin-react-dom](https://github.com/Rel1cx/eslint-react/tree/main/packages/plugins/eslint-plugin-react-dom) for React-specific lint rules:

```js
// eslint.config.js
import reactX from 'eslint-plugin-react-x'
import reactDom from 'eslint-plugin-react-dom'

export default tseslint.config({
  plugins: {
    // Add the react-x and react-dom plugins
    'react-x': reactX,
    'react-dom': reactDom,
  },
  rules: {
    // other rules...
    // Enable its recommended typescript rules
    ...reactX.configs['recommended-typescript'].rules,
    ...reactDom.configs.recommended.rules,
  },
})
```
