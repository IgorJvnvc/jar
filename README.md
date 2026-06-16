# Jar

Native-only mobile app (no PWA/web build) for tracking 8-ball pool sessions, duels, halls, and player stats. Android-first, with iOS to follow.

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core, Identity, JWT
- Database: PostgreSQL
- Frontend: React + Vite + TypeScript + Framer Motion
- Mobile: Capacitor Android/iOS wrapper (native-only; PWA/web Firebase SDK removed — push uses Capacitor + FCM)

## Monorepo

- `server/PoolTracker.Api` - .NET API and domain model
- `server/PoolTracker.Api.Tests` - integration test suite
- `client` - React + Capacitor mobile client

## Features

- JWT auth with player profiles (avatar, points balance, debt, titles, equipped cue).
- Sessions: start/stop play and log per-game reports (balls potted, games, snookers).
- Pool halls and tables with player ratings.
- Duels: challenge a player, settle by mutual agreement or coin-flip, wager points/debt.
- Leaderboards: Sessions and Duels (win-rate ranked).
- Player records: duel and general win/loss with win-rate.
- Cue shop with equippable cues.
- Real-time updates over SignalR; native push for duel challenges.

## Local development

Prerequisites:

- .NET SDK 8
- Node.js 22+
- PostgreSQL 14+

Run backend:

```bash
dotnet run --project server/PoolTracker.Api
```

Run client:

```bash
npm install --prefix client
npm run dev --prefix client
```

## Native push setup

- Android Firebase config: `client/android/app/google-services.json`
- iOS Firebase config: `client/ios/App/App/GoogleService-Info.plist`
- Android custom duel sound: `client/android/app/src/main/res/raw/duel_challenge.wav`
- Android notification channel id: `duel_challenges`

If Firebase credentials are not configured on backend, push send is skipped gracefully.

## Deploy backend to Railway (Docker)

Repository contains:

- `server/PoolTracker.Api/Dockerfile`
- `server/PoolTracker.Api/.dockerignore`
- `railway.toml`

Railway environment variables:

- `ConnectionStrings__DefaultConnection`
- `Jwt__SigningKey` (32+ chars)
- `Jwt__Issuer`
- `Jwt__Audience`
- `ALLOWED_ORIGINS` (comma-separated client origins)
- `FIREBASE_CREDENTIALS_PATH` (optional until Firebase admin key is added)
- `FIREBASE_PROJECT_ID` (optional)

Health check endpoint:

- `/health`

## Android signing

- Signing config is wired in `client/android/app/build.gradle` and reads `client/android/keystore.properties`.
- Template file: `client/android/keystore.properties.example`
- Keep real keystore and passwords out of git.

Build commands:

```bash
npm run build:mobile --prefix client
cd client/android
./gradlew assembleRelease
```

Signed APK output path:

- `client/android/app/build/outputs/apk/release/app-release.apk`

## Roadmap

- **Phase 1 (done)** — Duel game-feel and player identity.
- **Phase 2 (done)** — Player records and duel leaderboard.
- **Phase 3 (planned)** — Pool-day engine: background auto-stop/settlement, daily hall competition, retire manual stat editing.
- **Phase 4+ (planned)** — Levels/XP, achievements, cue stat bonuses, match simulation.
