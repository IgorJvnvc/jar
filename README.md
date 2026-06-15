# Jar

Native-first mobile app for tracking 8-ball pool sessions, duels, halls, and player stats.

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core, Identity, JWT
- Database: PostgreSQL
- Frontend: React + Vite + TypeScript + Framer Motion
- Mobile: Capacitor Android/iOS wrapper

## Monorepo

- `server/PoolTracker.Api` - .NET API and domain model
- `server/PoolTracker.Api.Tests` - integration test suite
- `client` - React + Capacitor mobile client

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
