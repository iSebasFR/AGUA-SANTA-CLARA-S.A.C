# HT-04 Sprint 2: Modelo de Datos Extendido

**Sistema de Pedidos y Distribución — Agua Santa Clara S.A.C.**

---

## INFORMACIÓN

| Campo | Detalle |
|---|---|
| Código del Proyecto | IS2-SPD |
| Historia Técnica | HT-04 Sprint 2 |
| Versión | 1.0 |
| Fecha | 30/09/2026 |

---

## 1. INTRODUCCIÓN

Este documento describe las **tablas nuevas y modificadas** en el Sprint 2, así como sus **relaciones**, para que el equipo conozca la estructura actual del modelo de datos.

Es **complementario** a la documentación de HT-04 Sprint 1 (Usuarios, Roles, Productos, Insumos, Clientes).

---

## 2. TABLAS NUEVAS

### 2.1 `local`
Representa las sedes físicas de la empresa.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_local` | BIGSERIAL | PK |
| `nombre` | VARCHAR(100) | UNIQUE, NOT NULL |
| `estado` | BOOLEAN | DEFAULT TRUE |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- 1:N con `producto_local` (un local tiene varios productos con stock)
- 1:N con `pedido` (un local despacha varios pedidos)

---

### 2.2 `producto_local`
Stock de cada producto por local.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_producto_local` | BIGSERIAL | PK |
| `id_local` | BIGINT | FK → local |
| `id_producto` | BIGINT | FK → producto |
| `stock` | INTEGER | CHECK >= 0 |
| `estado` | BOOLEAN | DEFAULT TRUE |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `local`
- N:1 con `producto`
- UNIQUE (`id_local`, `id_producto`)

---

### 2.3 `repartidor`
Personas que entregan los pedidos.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_repartidor` | BIGSERIAL | PK |
| `nombre` | VARCHAR(150) | NOT NULL |
| `celular` | VARCHAR(11) | UNIQUE, CHECK `^519[0-9]{8}$` |
| `estado` | BOOLEAN | DEFAULT TRUE |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- 1:N con `pedido` (un repartidor entrega varios pedidos)

---

### 2.4 `pedido`
Cabecera del pedido.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_pedido` | BIGSERIAL | PK |
| `id_local` | BIGINT | FK → local, NOT NULL |
| `id_repartidor` | BIGINT | FK → repartidor, NULL |
| `fecha_entrega` | TIMESTAMP | NOT NULL |
| `estado` | VARCHAR(20) | CHECK (Pendiente, Enviado, Entregado, Con Incidencia, Pagado, Pago Parcial) |
| `total` | NUMERIC(12,2) | CHECK >= 0 |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `local`
- N:1 con `repartidor`
- 1:N con `pedido_cliente`
- 1:N con `incidencia`
- 1:N con `pago`
- 1:N con `deuda`

---

### 2.5 `pedido_cliente`
Clientes que participan en un pedido (con su dirección de entrega).

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_pedido_cliente` | BIGSERIAL | PK |
| `id_pedido` | BIGINT | FK → pedido |
| `id_cliente` | BIGINT | FK → cliente |
| `id_direccion` | BIGINT | FK → direccion_cliente |
| `subtotal` | NUMERIC(12,2) | CHECK >= 0 |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `pedido`
- N:1 con `cliente`
- N:1 con `direccion_cliente`
- 1:N con `detalle_pedido`
- UNIQUE (`id_pedido`, `id_cliente`, `id_direccion`)

---

### 2.6 `detalle_pedido`
Productos que pide cada cliente.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_detalle_pedido` | BIGSERIAL | PK |
| `id_pedido_cliente` | BIGINT | FK → pedido_cliente |
| `id_producto` | BIGINT | FK → producto |
| `cantidad` | INTEGER | CHECK > 0 |
| `precio_unitario` | NUMERIC(12,2) | CHECK > 0 |
| `subtotal` | NUMERIC(12,2) | CHECK >= 0 |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `pedido_cliente`
- N:1 con `producto`

---

### 2.7 `incidencia`
Incidencias reportadas en un pedido.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_incidencia` | BIGSERIAL | PK |
| `id_pedido` | BIGINT | FK → pedido |
| `motivo` | VARCHAR(50) | CHECK (Cliente ausente, Dirección incorrecta, Producto dañado, Vehículo descompuesto, Otros) |
| `detalle` | VARCHAR(500) | NULL |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `pedido`

---

### 2.8 `metodo_pago`
Catálogo de métodos de pago.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_metodo_pago` | BIGSERIAL | PK |
| `nombre` | VARCHAR(50) | UNIQUE, NOT NULL |
| `estado` | BOOLEAN | DEFAULT TRUE |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Valores iniciales:** Efectivo, Yape, Transferencia Bancaria.

**Relaciones:**
- 1:N con `pago`

---

### 2.9 `pago`
Pagos individuales por cliente.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_pago` | BIGSERIAL | PK |
| `id_pedido` | BIGINT | FK → pedido |
| `id_cliente` | BIGINT | FK → cliente |
| `id_metodo_pago` | BIGINT | FK → metodo_pago |
| `monto` | NUMERIC(12,2) | CHECK > 0 |
| `fecha_pago` | TIMESTAMPTZ | DEFAULT NOW |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `pedido`
- N:1 con `cliente`
- N:1 con `metodo_pago`

---

### 2.10 `deuda`
Deudas de clientes con fecha de vencimiento.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_deuda` | BIGSERIAL | PK |
| `id_pedido` | BIGINT | FK → pedido |
| `id_cliente` | BIGINT | FK → cliente |
| `monto` | NUMERIC(12,2) | CHECK > 0 |
| `fecha_vencimiento` | DATE | NOT NULL |
| `estado` | VARCHAR(20) | CHECK (Pendiente, Pagada, Vencida) |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `pedido`
- N:1 con `cliente`

---

### 2.11 `movimiento_bidones`
Balance de bidones retornables por cliente.

| Columna | Tipo | Restricciones |
|---|---|---|
| `id_movimiento` | BIGSERIAL | PK |
| `id_cliente` | BIGINT | FK → cliente |
| `bidones_entregados` | INTEGER | CHECK >= 0 |
| `bidones_devueltos` | INTEGER | CHECK >= 0 |
| `saldo` | INTEGER | |
| `estado_registro` | BOOLEAN | DEFAULT TRUE |
| `fecha_creacion` | TIMESTAMPTZ | DEFAULT NOW |
| `fecha_actualizacion` | TIMESTAMPTZ | NULL |

**Relaciones:**
- N:1 con `cliente`

---

## 3. MODIFICACIONES A TABLAS EXISTENTES

### 3.1 `producto` — columnas agregadas

| Columna | Tipo | Descripción |
|---|---|---|
| `es_retornable` | BOOLEAN | Indica si el producto es retornable (bidones) |
| `categoria` | VARCHAR(50) | Categoría del producto |

### 3.2 `insumo` — columnas agregadas

| Columna | Tipo | Restricciones |
|---|---|---|
| `stock_actual` | INTEGER | CHECK >= 0 |
| `stock_minimo` | INTEGER | CHECK >= 0 |

### 3.3 `cliente` — columnas agregadas

| Columna | Tipo | Descripción |
|---|---|---|
| `saldo_bidones` | INTEGER | Saldo de bidones pendientes |
| `deuda_total` | NUMERIC(12,2) | Deuda total acumulada |

---

## 4. MAPA DE RELACIONES

Resumen de las relaciones entre tablas:

| Tabla origen | Tipo | Tabla destino | Descripción |
|---|---|---|---|
| `local` | 1:N | `producto_local` | Un local tiene varios productos con stock |
| `producto` | 1:N | `producto_local` | Un producto puede estar en varios locales |
| `local` | 1:N | `pedido` | Un local despacha varios pedidos |
| `repartidor` | 1:N | `pedido` | Un repartidor entrega varios pedidos |
| `pedido` | 1:N | `pedido_cliente` | Un pedido tiene varios clientes |
| `cliente` | 1:N | `pedido_cliente` | Un cliente puede estar en varios pedidos |
| `direccion_cliente` | 1:N | `pedido_cliente` | Una dirección puede recibir varios pedidos |
| `pedido_cliente` | 1:N | `detalle_pedido` | Cada cliente tiene varios productos |
| `producto` | 1:N | `detalle_pedido` | Un producto puede estar en varios detalles |
| `pedido` | 1:N | `incidencia` | Un pedido puede tener varias incidencias |
| `pedido` | 1:N | `pago` | Un pedido puede tener varios pagos |
| `cliente` | 1:N | `pago` | Un cliente puede hacer varios pagos |
| `metodo_pago` | 1:N | `pago` | Un método puede usarse en varios pagos |
| `pedido` | 1:N | `deuda` | Un pedido puede generar varias deudas |
| `cliente` | 1:N | `deuda` | Un cliente puede tener varias deudas |
| `cliente` | 1:N | `movimiento_bidones` | Un cliente tiene varios movimientos de bidones |

## 5. MIGRACIONES APLICADAS

| Migración | Qué agrega |
|---|---|
| `InitialCreate` | Tablas del Sprint 1 (rol, permiso, usuario, producto, insumo, cliente, direccion_cliente) |
| `AddModuloSprint2` | local, producto_local, repartidor, pedido, pedido_cliente, detalle_pedido + dni en cliente + url_ubicacion en direccion_cliente |
| `AddModuloSprint2Parte2` | incidencia, metodo_pago, pago, deuda, movimiento_bidones + es_retornable y categoria en producto + stock_actual y stock_minimo en insumo + saldo_bidones y deuda_total en cliente |

---

## 6. CONVENCIONES

| Elemento | Convención | Ejemplo |
|---|---|---|
| Tablas | `snake_case`, plural o singular | `pedido_cliente`, `metodo_pago` |
| Columnas | `snake_case` | `fecha_creacion`, `id_local` |
| PK | `id_<entidad>` | `id_pedido`, `id_cliente` |
| FK | `id_<entidad_referenciada>` | `id_local`, `id_repartidor` |
| Índices | `idx_<tabla>_<columna>` | `idx_pedido_estado` |
| Constraints | `chk_`, `uq_`, `fk_` | `chk_pago_monto_positivo` |
| Entidades C# | `PascalCase` | `Pedido`, `MetodoPago` |

---

## 7. ESTADOS DEL SISTEMA

### Estados de pedido
`Pendiente`, `Enviado`, `Entregado`, `Con Incidencia`, `Pagado`, `Pago Parcial`

### Estados de deuda
`Pendiente`, `Pagada`, `Vencida`

### Motivos de incidencia
`Cliente ausente`, `Dirección incorrecta`, `Producto dañado`, `Vehículo descompuesto`, `Otros`

### Métodos de pago
`Efectivo`, `Yape`, `Transferencia Bancaria`

### Locales
`Santa Rosa`, `Niño Jesús`, `Apurímac`

---

**Fin del documento.**