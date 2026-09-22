# Constraints, PKs y FKs — HT-04

## Reglas de negocio
| Tabla | Constraint | Regla |
|---|---|---|
| producto | chk_producto_precio_positivo | precio_venta > 0 |
| producto | chk_producto_costo_positivo | costo > 0 |
| producto | chk_producto_stock_no_negativo | stock_actual >= 0 AND stock_minimo >= 0 |
| insumo | chk_insumo_costo_positivo | costo > 0 |
| usuario | uq_usuario_username | username UNIQUE |
| usuario | uq_usuario_email | email UNIQUE |
| rol | uq_rol_codigo | codigo UNIQUE |
| permiso | uq_permiso_codigo | codigo UNIQUE |

## Políticas ON DELETE
| Relación | Política | Motivo |
|---|---|---|
| usuario → rol | RESTRICT | No borrar roles con usuarios |
| direccion_cliente → cliente | CASCADE | Direcciones dependen del cliente |
| rol_permiso → rol | CASCADE | Tabla intermedia |
| rol_permiso → permiso | CASCADE | Tabla intermedia |