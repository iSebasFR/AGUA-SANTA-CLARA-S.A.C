# Normalización hasta FNBC — HT-04

## 1FN — Valores atómicos
- Todas las columnas contienen un solo valor.
- Las direcciones múltiples se separaron en `direccion_cliente`.

## 2FN — Sin dependencias parciales
- `rol_permiso` tiene PK compuesta (`id_rol`, `id_permiso`) sin atributos parciales.

## 3FN — Sin dependencias transitivas
- En `usuario` no se guarda el nombre del rol, solo la FK `id_rol`.

## FNBC — Todo determinante es clave candidata
- `username` y `email` son UNIQUE en `usuario`.
- `codigo` es UNIQUE en `rol` y `permiso`.