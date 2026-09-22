# Diccionario de Datos — HT-04

## Tabla: usuario
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| id_usuario | BIGSERIAL | PK | Identificador |
| id_rol | BIGINT | FK, NOT NULL | Rol asignado |
| nombres | VARCHAR(100) | NOT NULL | Nombres |
| apellidos | VARCHAR(100) | NOT NULL | Apellidos |
| username | VARCHAR(50) | UNIQUE, NOT NULL | Usuario login |
| email | VARCHAR(150) | UNIQUE, NOT NULL | Correo |
| password_hash | VARCHAR(255) | NOT NULL | Contraseña hash |
| estado | BOOLEAN | DEFAULT TRUE | Activo/Inactivo |
| estado_registro | BOOLEAN | DEFAULT TRUE | Vigencia |
| fecha_creacion | TIMESTAMPTZ | DEFAULT NOW | Auditoría |
| fecha_actualizacion | TIMESTAMPTZ | NULL | Auditoría |

## Tabla: producto
| Columna | Tipo | Restricciones | Descripción |
|---|---|---|---|
| id_producto | BIGSERIAL | PK | Identificador |
| nombre | VARCHAR(150) | NOT NULL | Nombre |
| descripcion | VARCHAR(255) | | Descripción |
| precio_venta | NUMERIC(12,2) | CHECK > 0 | Precio |
| costo | NUMERIC(12,2) | CHECK > 0 | Costo |
| stock_actual | INTEGER | >= 0 | Stock actual |
| stock_minimo | INTEGER | >= 0 | Stock mínimo |
| estado | BOOLEAN | DEFAULT TRUE | Activo/Inactivo |
| estado_registro | BOOLEAN | DEFAULT TRUE | Vigencia |
| fecha_creacion | TIMESTAMPTZ | DEFAULT NOW | Auditoría |
| fecha_actualizacion | TIMESTAMPTZ | NULL | Auditoría |

*(Repite para rol, permiso, rol_permiso, insumo, cliente, direccion_cliente)*