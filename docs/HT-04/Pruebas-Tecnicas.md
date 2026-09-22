# Pruebas Técnicas — HT-04

## 1. Integridad referencial
- ✅ FK usuario → rol con ON DELETE RESTRICT
- ✅ FK direccion_cliente → cliente con ON DELETE CASCADE

## 2. Normalización
- ✅ 1FN, 2FN, 3FN, FNBC verificadas

## 3. Constraints
- ✅ CHECK precio_venta > 0 funciona
- ✅ CHECK costo > 0 funciona
- ✅ UNIQUE username funciona

## 4. Auditoría
- ✅ fecha_creacion se setea automáticamente
- ✅ fecha_actualizacion se setea al modificar

## 5. Seed de roles
- ✅ Roles: Administradora, Vendedora, Gerente insertados

## 6. Conexión EF Core
- ✅ Endpoint /Test/Conexion devuelve status OK