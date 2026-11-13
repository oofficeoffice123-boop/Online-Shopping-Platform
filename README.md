# Z Square

Z Square is a Pakistani clothing store with a React storefront and an ASP.NET
Core API. The backend is split into Domain, Application, Infrastructure, and
API projects, with SQL Server persistence through Entity Framework Core.

## Features

- Product browsing, search, filtering, and product reviews.
- Customer registration and login, cart, saved addresses, checkout, order
  history, and order cancellation.
- Staff tools for products, variants, stock, images, orders, discounts,
  restocking, and sales reporting.
- Admin tools for user management, audit history, and review moderation.
- Role-based authorization for customers, managers, and administrators.

## Requirements

- .NET 8 SDK
- SQL Server (LocalDB is the default for local development)
- Node.js and npm

## Run locally

1. Configure the database connection string and JWT signing key as described
   below.
2. Apply the database migrations from the repository root:

   ```powershell
   dotnet ef database update --project ZSquare.Infrastructure --startup-project ZSquare.API
   ```

3. Start the API:

   ```powershell
   dotnet run --project ZSquare.API
   ```

   In Development, the API seeds reference data and development accounts.
   Swagger is available at `/swagger`.

4. In another terminal, start the React app:

   ```powershell
   cd Frontend
   npm install
   npm run dev
   ```

   The frontend uses `http://localhost:5264` as its default API URL. Set
   `VITE_API_BASE_URL` to override it. Add the frontend origin to
   `Cors:AllowedOrigins` if it differs from the configured local origins.

## Configuration

The API reads its SQL Server connection string from
`ConnectionStrings:DefaultConnection`. The checked-in default uses LocalDB.
For other environments, set `ConnectionStrings__DefaultConnection` in the
environment.

Set `Jwt:SigningKey` to a secret of at least 32 characters. For local
development, configure it with user-secrets:

```powershell
dotnet user-secrets set --project ZSquare.API/ZSquare.API.csproj "Jwt:SigningKey" "your-local-secret-at-least-32-characters"
```

Development accounts are seeded when their corresponding password settings
are provided. Configure `Auth:SeedPasswords:Admin`,
`Auth:SeedPasswords:Manager`, and `Auth:SeedPasswords:Customer` with
user-secrets. Their email addresses are `admin@zsquare.local`,
`manager@zsquare.local`, and `customer@zsquare.local`.

## Authentication and security

- Access tokens are JWTs with a 15-minute lifetime. Issuer, audience, lifetime,
  and signing key are validated.
- Refresh tokens are cryptographically random, valid for seven days, and
  stored in the database as SHA-256 hashes. Refresh rotates the token; reuse
  of a revoked token revokes all refresh tokens for that user.
- The refresh token is sent in an HttpOnly, SameSite=Strict cookie. The cookie
  is Secure outside local HTTP development. Access tokens are returned in the
  authentication response.
- Passwords use ASP.NET Core's `PasswordHasher<User>`. Public registration
  always creates a Customer account.
- Login and registration requests are rate limited. CORS allows the origins
  configured in `Cors:AllowedOrigins`.

## Khaadi product import

The API includes an idempotent importer for a Khaadi workbook and its product
images. Configure `Import:ExcelPath` and `Import:ImagesSourcePath` to point to
the workbook and image collection on your machine. Apply all database
migrations before importing, then run:

```powershell
dotnet run --project ZSquare.API -- import
```

The importer maps workbook IDs to product codes, normalizes colors, assigns
categories from product names, creates size variants, and copies up to four
images per product into the API's static image directory. Existing product
codes are skipped. Missing images and unmatched categories are reported in
the import summary. Generated image files are local data and are not committed
to Git.
