# Auditoría Estándar — HT-04

## Campos de auditoría
| Campo | Tipo | Descripción |
|---|---|---|
| fecha_creacion | TIMESTAMPTZ | Fecha de alta del registro |
| fecha_actualizacion | TIMESTAMPTZ | Última modificación |
| estado | BOOLEAN | Activo/Inactivo (negocio) |
| estado_registro | BOOLEAN | Vigencia lógica (soft delete) |

## Implementación
Override de `SaveChanges()` en `AppDbContext`:
- Al agregar (Added): setea `FechaCreacion = UtcNow`
- Al modificar (Modified): setea `FechaActualizacion = UtcNow`