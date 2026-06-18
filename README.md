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

### Database migrations

The API uses EF Core migrations. On startup the app applies any pending migrations
automatically against Postgres; the integration tests provision their SQLite schema
directly from the model instead.

Create a migration after changing the domain model or `PoolTrackerDbContext`:

```bash
dotnet tool install --global dotnet-ef --version 8.0.24   # one-time
dotnet ef migrations add <Name> --project server/PoolTracker.Api
```

A design-time factory (`PoolTrackerDbContextFactory`) supplies the Npgsql provider and
connection string, so migrations can be generated without starting the web host.

> One-time reset: dev databases created before migrations were adopted (via
> `EnsureCreated`) have no migration history and will fail `MigrateAsync` on startup.
> Drop and recreate the dev database once so the baseline `InitialCreate` migration can
> apply cleanly:
>
> ```bash
> dropdb -U postgres pooltracker_dev && createdb -U postgres pooltracker_dev
> ```

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
- **Phase 3 (done)** — Pool-day engine: background auto-stop/settlement, daily hall competition, retire manual stat editing.
- **Phase 4a (done)** — Live per-game session tracking, per-session skill scoring (power/accuracy/cue control/spin), golden-break bonus, and cue-aware effective stats.
- **Phase 4b (done)** — Battle types (1v1/2v2) and 10-ball support, with per-mode accuracy tables and a 9-/10-ball "train" that waives a low-pot accuracy penalty.
- **Phase 4c (done)** — Per-rack detail surfaced on completed sessions, with an expandable rack-by-rack breakdown in the session history.
- **Phase 4d (done)** — Revised accuracy scoring: retuned per-mode tables, a 9-/10-ball break-pot accuracy bonus (+1 per break pot, on top of power), and a two-sided "train" (potting the money ball early hard-sets accuracy to +0.5 on a win, −0.5 on a loss).
- **Phase 5 (done)** — Player levels and XP: a steep exponential curve with cumulative experience earned from pool-day sessions (games, wins, pots, snooker escapes, table time, golden breaks) and duel wins, surfaced as a level, progress bar, and western title on the profile and leaderboards (the "Himen Healer" debt title still takes precedence while in debt).
- **Phase 5+ (planned)** — Achievements and match simulation.
