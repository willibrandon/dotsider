# RichLibraryV2

Breaking-changes version of RichLibrary (v3.0.0) with the same assembly name. Used as the "right" side in assembly diff tests against RichLibrary v2.5.1.

Key changes from v1:
- `User` — added `PhoneNumber`, `LastLoginAt`
- `Product` — renamed `StockCount` → `Quantity`, added `Sku`
- `UserRole` — added `Moderator` value
- `UserService` — dropped `IRepository<User>`, changed return types, added `FindByTag`
- `ProductCatalog` — added `maxResults` parameter to `Search`, added `GetPage`
- New types: `Order`, `OrderLine`, `AuditLog`, `OrderService`
