CREATE TABLE insumo (
    id_insumo BIGSERIAL PRIMARY KEY,
    nombre VARCHAR(150) NOT NULL,
    descripcion VARCHAR(255),
    costo NUMERIC(12,2) NOT NULL,
    estado BOOLEAN NOT NULL DEFAULT TRUE,
    estado_registro BOOLEAN NOT NULL DEFAULT TRUE,
    fecha_creacion TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_actualizacion TIMESTAMPTZ,
    CONSTRAINT chk_insumo_costo_positivo CHECK (costo > 0)
);