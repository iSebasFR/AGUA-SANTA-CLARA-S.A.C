# Entidades EF Core — HT-04

## Entidades configuradas
- Rol, Permiso, RolPermiso
- Usuario
- Producto, Insumo
- Cliente, DireccionCliente

## Configuraciones
Cada entidad tiene su `IEntityTypeConfiguration<T>` en `Models/Configurations/`.

## DbContext
`AppDbContext` hereda de `IdentityDbContext<Usuario, Rol, long>`.