# Z SQUARE

Mini Pakistani clothing store built with ASP.NET Core 8, EF Core 8, SQL Server,
and a React frontend planned for a later phase. The solution uses Domain,
Application, Infrastructure, and API projects.

## Authentication

- Register, login, refresh, logout, and `/api/auth/me` are implemented.
- Access tokens are JWTs with a 15-minute lifetime and NameIdentifier, Email,
  and Role claims. Issuer, audience, lifetime, and signing key are validated
  with zero clock skew.
- Refresh tokens are 64 cryptographically random bytes encoded as Base64,
  valid for 7 days, and stored only as SHA-256 hashes. Every refresh revokes
  the old token, issues a new one, and stores its hash in
  `ReplacedByTokenHash`. Reuse of a revoked token revokes all tokens belonging
  to that user.
- Refresh tokens are delivered only in an HttpOnly, Secure, SameSite=Strict
  cookie scoped to `/api/auth`; access tokens are returned in JSON.
- Passwords use ASP.NET Core `PasswordHasher<User>`. Registration always
  assigns Customer; Admin, Manager, and Customer development accounts are
  seeded from local configuration.
- Authorization policies: `StaffOnly` (Manager or Admin), `AdminOnly`, and
  `CustomerOnly`.
  Ownership checks derive user IDs from the access token, never the request
  body. Role is never accepted during public registration.
- JWT keys and connection strings come from user-secrets or environment
  variables. CORS requires exactly one React origin, and login and registration
  are rate limited by IP.

For local development, configure `Jwt:SigningKey` (at least 32 characters)
and `Auth:SeedPasswords:Admin`, `Auth:SeedPasswords:Manager`, and
`Auth:SeedPasswords:Customer` with `dotnet user-secrets set --project
ZSquare.API/ZSquare.API.csproj`. Seeded account emails are
`admin@zsquare.local`, `manager@zsquare.local`, and
`customer@zsquare.local`. The refresh-token cleanup service removes tokens
that expired more than 30 days ago.

## Khaadi import design

Source folder: `C:\Users\zain.zia\Downloads\Khaadi_Data`. The workbook path
comes from `Import:ExcelPath`; image folders come from
`Import:ImagesSourcePath`. The workbook has one `khaadi_data` sheet and 400
rows with columns `ID`, `Product Name`, `Product Description`, `Price`,
`Availability`, `Color`, and `Img Path`. Product Link is not present.

- Map `ID` to unique `ProductCode`. Product Name is a type, so it is not
  unique. Ignore Availability, which is always `In Stock`.
- Price is integer PKR. Store it in the existing decimal money fields; log
  price outliers without changing or skipping them.
- Normalize color casing, expand `L-` and `D-` prefixes to `Light` and
  `Dark`, and store `MULTI` as `Multi`.
- Map product names to categories using these keyword groups, evaluating
  more specific rules first: Unstitched Fabric (`Fabrics`); Dresses (`Dress`,
  `Jumpsuit`); Kurtas and Kameez (`Kameez`, `Kurta`, `Choga`, `Kaftan`);
  Shirts and Tops (`Shirt`, `T-shirt`, `Blouse`, `Tunic`, `Peplum`, `Polo`,
  `Tank Top`, `Camisole`, `Sweatshirt`, `Drop Shoulder`, `Raglan`); Bottoms
  (`Shalwar`, `Trousers`, `Pants`, `Culottes`, `Jeans`, `Skirt`); Dupattas and
  Shawls (`Dupatta`, `Stole`, `Shawl`). Log unmatched rows; do not assign a
  default category silently.
- Skip existing ProductCodes to keep imports idempotent. Generate XS, S, M,
  L, XL, and XXL variants for stitched products; generate One Size for
  unstitched fabric, dupatta, stole, and shawl. Use the row's color and
  generated random stock, including a few zero-stock variants.
- For each product ID, read its image folder, filter to image extensions,
  natural-sort filenames, and copy the first four images into
  `wwwroot/images/<ProductCode>/`. Add four image rows with the first primary,
  sort orders 1 through 4, and forward-slash URLs such as
  `/images/ALK231009/1.jpg`. If the folder is missing or empty, log and
  continue. Sample folders belong under `/SeedData/sample-images`.

Imported images in `wwwroot/images` are generated local data and are ignored
by Git. Run the importer with `dotnet run --project ZSquare.API -- import`
after applying the migration and configuring the import paths.
