# FridgeShare

FridgeShare is a shared fridge for a household. Roommates join the same household, keep track of what is in the fridge and when it expires, share a shopping list, and stay within a monthly food budget.

The project is made for the course *App-programmering III* (niveau 4).

## Features

- **Households.** Create a household or join one with an invite code. Invite codes expire after a while, and the owner can make a new one.
- **Fridge inventory.** Add items with quantity, price and expiry date. Mark items as used or thrown out.
- **Barcode scanning.** Scan a product with the camera. The API looks up the name and picture on [Open Food Facts](https://world.openfoodfacts.org/) and caches the result.
- **Shopping list.** A shared list for the household. Checked items can be cleared in one go.
- **Monthly budget.** If adding an item would push this month's spending over the budget, the API answers `409 over_budget` and the app asks "add anyway?".
- **Stats.** Spending and food waste (used vs. thrown out) for the household.
- **Accounts.** Register and log in with email and password. The API issues its own JWT tokens, and login/register is rate limited.

## Tech stack

| Part | Folder | Technology |
| --- | --- | --- |
| Mobile/web app | [App-prog/](App-prog/) | Expo SDK 58, React Native, Expo Router, TypeScript |
| Web API | [Api/](Api/) | ASP.NET Core (.NET 10), EF Core, JWT auth |
| Database | — | PostgreSQL on Supabase (tables in the `app` schema) |
| API tests | [Api.Tests/](Api.Tests/) | xUnit, `WebApplicationFactory`, Testcontainers (PostgreSQL) |
| App tests | [App-prog/src/lib/\_\_tests\_\_/](App-prog/src/lib/__tests__/) | Jest (`jest-expo`) |

## Project structure

```
.
├── Api/                  ASP.NET Core Web API
│   ├── Controllers/      Auth, Households, Items, Shopping, Products
│   ├── Data/             AppDbContext (EF Core)
│   ├── Migrations/       EF Core migrations (applied on startup)
│   ├── Models/           User, Household, Item, ShoppingItem, ProductCache
│   └── Services/         Tokens, budget check, invite codes, Open Food Facts
├── Api.Tests/            Integration tests for the API
├── App-prog/             Expo app
│   └── src/
│       ├── app/          Screens (file-based routing)
│       ├── components/   UI components, barcode scanner, theme
│       ├── context/      Auth and household state
│       └── lib/          API client, budget/expiry/format helpers
├── docker-compose.yml    Optional local PostgreSQL
└── AppProgrammering.slnx Solution file (Api + Api.Tests)
```

## Getting started

### Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (LTS)
- [Docker](https://www.docker.com/) (needed for the API tests, and for a local database if you don't use Supabase)
- The [Expo Go](https://expo.dev/go) app on your phone, or an Android/iOS emulator

### 1. Configure the API

The API reads secrets from `Api/.env`. This file is ignored by git, so copy [Api/.env.example](Api/.env.example) to `Api/.env` and fill it in:

```env
# Supabase: Project Settings → Database → Session pooler connection string
ConnectionStrings__Default=Host=...;Port=5432;Database=postgres;Username=...;Password=...

# JWT signing key, at least 32 characters
Jwt__Key=change-me-to-a-long-random-secret-value
```

To use a local database instead of Supabase, start the one in `docker-compose.yml`:

```bash
docker compose up -d
```

and use this connection string:

```env
ConnectionStrings__Default=Host=localhost;Port=5432;Database=appdb;Username=app;Password=app_dev_password
```

### 2. Run the API

```bash
cd Api
dotnet run
```

The API listens on `http://localhost:5258` (and on all network interfaces, so a phone on the same Wi-Fi can reach it). Database migrations are applied automatically on startup.

In development, Swagger UI is available at <http://localhost:5258/swagger>.

### 3. Run the app

```bash
cd App-prog
npm install
npx expo start
```

Then scan the QR code with Expo Go, or press `a` (Android), `i` (iOS) or `w` (web).

The app finds the API by itself:

- **Web:** `http://localhost:5258`
- **Phone on the same Wi-Fi (LAN mode):** the IP of the computer running Expo, port 5258
- **Android emulator:** `http://10.0.2.2:5258`

If you use `npx expo start --tunnel`, or the API runs somewhere else, copy [App-prog/.env.example](App-prog/.env.example) to `App-prog/.env.local` and set the URL:

```env
EXPO_PUBLIC_API_URL=https://your-api-host
```

## Tests

**API** (Docker must be running; Testcontainers starts a throwaway PostgreSQL):

```bash
dotnet test
```

**App:**

```bash
cd App-prog
npm test
```

## API overview

All endpoints except register and login need an `Authorization: Bearer <token>` header.

| Method | Endpoint | Description |
| --- | --- | --- |
| POST | `/api/auth/register` | Create an account |
| POST | `/api/auth/login` | Log in, returns a JWT |
| GET | `/api/auth/me` | Current user |
| GET / POST | `/api/households` | List my households / create one |
| POST | `/api/households/join` | Join with an invite code |
| GET / PUT | `/api/households/{id}` | Get / update a household (name, budget) |
| POST | `/api/households/{id}/invite-code` | Make a new invite code |
| DELETE | `/api/households/{id}/members/me` | Leave a household |
| GET | `/api/households/{id}/budget` | This month's spending vs. budget |
| GET | `/api/households/{id}/stats` | Spending and waste stats |
| GET / POST | `/api/households/{id}/items` | List / add fridge items |
| GET / PATCH / DELETE | `/api/households/{id}/items/{itemId}` | Get / edit / delete an item |
| POST | `/api/households/{id}/items/{itemId}/use` | Mark as used |
| POST | `/api/households/{id}/items/{itemId}/throw-out` | Mark as thrown out |
| GET / POST | `/api/households/{id}/shopping` | Shopping list / add to it |
| PATCH / DELETE | `/api/households/{id}/shopping/{itemId}` | Check off / remove |
| DELETE | `/api/households/{id}/shopping/checked` | Remove all checked items |
| GET | `/api/products/{barcode}` | Look up a product by barcode |

See Swagger UI for request and response details.
