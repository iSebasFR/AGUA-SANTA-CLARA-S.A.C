CREATE TABLE producto (
    id_producto BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    descripcion VARCHAR(255),
    precio_venta NUMERIC(12,2) NOT NULL,
    costo NUMERIC(12,2) NOT NULL,
    stock_actual INTEGER NOT NULL DEFAULT 0,
    stock_minimo INTEGER NOT NULL DEFAULT 0,
    estado BOOLEAN NOT NULL DEFAULT TRUE,
    estado_registro BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion TIMESTAMPTZ,
    CONSTRAINT chk_producto_precio_positivo CHECK (precio_venta > 0),
    CONSTRAINT chk_producto_costo_positivo CHECK (costo > 0),
    CONSTRAINT chk_producto_stock_no_negativo CHECK (stock_actual >= 0 AND stock_minimo >= 0)
);