# DER del Sprint 1 — HT-04

## Descripción

Modelo Entidad-Relación de las tablas del Sprint 1.

## Diagrama

\`\`\`mermaid
erDiagram
    ROL ||--o{ USUARIO : "tiene"
    ROL ||--o{ ROL_PERMISO : "asigna"
    PERMISO ||--o{ ROL_PERMISO : "incluye"
    CLIENTE ||--o{ DIRECCION_CLIENTE : "tiene"

    ROL {
        bigint id_rol PK
        varchar codigo
        varchar nombre
        boolean estado
    }
    USUARIO {
        bigint id_usuario PK
        bigint id_rol FK
        varchar username
        varchar email
        varchar password_hash
        boolean estado
    }
    PERMISO {
        bigint id_permiso PK
        varchar codigo
        varchar nombre
    }
    ROL_PERMISO {
        bigint id_rol FK
        bigint id_permiso FK
    }
    PRODUCTO {
        bigint id_producto PK
        varchar nombre
        decimal precio_venta
        decimal costo
        int stock_actual
        int stock_minimo
    }
    INSUMO {
        bigint id_insumo PK
        varchar nombre
        decimal costo
    }
    CLIENTE {
        bigint id_cliente PK
        varchar nombre
        varchar telefono
        boolean estado
    }
    DIRECCION_CLIENTE {
        bigint id_direccion PK
        bigint id_cliente FK
        varchar direccion
        boolean principal
    }
\`\`\`