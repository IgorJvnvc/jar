# Jar Client

React + Vite + TypeScript front end for Jar, packaged as a native Android/iOS app
via Capacitor. Native-only — there is no PWA or web/service-worker build.

## Scripts

| Script | What it does |
| --- | --- |
| `npm run dev` | Vite dev server (browser, port 5173) for fast UI iteration |
| `npm run build` | Type-check (`tsc -b`) + production Vite build to `dist/` |
| `npm run build:mobile` | `build` then `npx cap sync` into the native projects |
| `npm run cap:sync` | Sync web assets + plugins into Android/iOS (no rebuild) |
| `npm run cap:android` | Open the Android project in Android Studio |
| `npm run cap:ios` | Open the iOS project in Xcode |
| `npm run lint` | ESLint (flat config) |
| `npm run test` / `test:watch` | Vitest (jsdom) for `src/lib` unit tests |
| `npm run preview` | Preview the production build locally |

## Stack notes

- React 19 + React Router 7, Framer Motion for animation, SignalR for real-time.
- Native push via `@capacitor/push-notifications` (FCM on Android) — no web push.
- ESLint uses `eslint-plugin-react-hooks` v7 (React-Compiler-aligned): avoid
  synchronous `setState` inside effects — derive state instead.
- Unit tests cover `src/lib` helpers only; there are no component/render tests.

## Mobile workflow

1. `npm run build:mobile` to build and sync into the native projects.
2. `npm run cap:android` / `npm run cap:ios` to open and run on device/emulator.

Backend setup, deployment, native push config, and Android signing live in the
[root README](../README.md).
