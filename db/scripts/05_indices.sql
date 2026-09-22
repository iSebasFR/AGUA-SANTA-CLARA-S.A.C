CREATE INDEX idx_usuario_username ON usuario(username);
CREATE INDEX idx_usuario_rol ON usuario(id_rol);
CREATE INDEX idx_producto_nombre ON producto(nombre);
CREATE INDEX idx_insumo_nombre ON insumo(nombre);
CREATE INDEX idx_cliente_nombre ON cliente(nombre);
CREATE INDEX idx_cliente_telefono ON cliente(telefono);
CREATE INDEX idx_direccion_cliente ON direccion_cliente(id_cliente);