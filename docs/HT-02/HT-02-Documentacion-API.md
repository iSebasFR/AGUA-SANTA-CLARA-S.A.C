# HT-02: Backend y API REST (.NET 10)

**Sistema de Pedidos y Distribución — Agua Santa Clara S.A.C.**

---

## CONTROL DE VERSIONES

| Versión | Hecha por | Revisada por | Aprobada por | Fecha | Motivo |
|---|---|---|---|---|---|
| 1.0 | Equipo de Desarrollo | Líder Técnico | Product Owner | 26/09/2026 | Creación del documento |

---

## INFORMACIÓN DEL PROYECTO

| Campo | Detalle |
|---|---|
| **Código del Proyecto** | IS2-SPD |
| **Nombre del Proyecto** | Sistema de Pedidos y Distribución |
| **Empresa** | Agua Santa Clara S.A.C. |
| **Historia Técnica** | HT-02 — Configuración de la Arquitectura Backend y API REST |
| **Versión del Documento** | 1.0 |
| **Fecha de Emisión** | 26/09/2026 |

---

## 1. INTRODUCCIÓN

El presente documento describe la configuración de la arquitectura Backend y la API REST del Sistema de Pedidos y Distribución de Agua Santa Clara S.A.C., desarrollada sobre ASP.NET Core .NET 10.

Su propósito es documentar la estructura de la API, los endpoints implementados, la configuración de Swagger y las evidencias de verificación.

---

## 2. OBJETIVOS

- Configurar la API REST sobre el proyecto MVC existente.
- Implementar Swagger para documentación automática de endpoints.
- Crear un endpoint de prueba para verificar la comunicación.
- Validar la respuesta HTTP y el formato JSON.

---

## 3. ALCANCE DE LA HT-02

| Código | Tarea |
|---|---|
| T27 | Crear proyecto backend .NET 10 |
| T28 | Estructurar capas del backend |
| T29 | Configurar API REST |
| T30 | Crear endpoint de prueba |
| T31 | Verificar respuesta de la API |

---

## 4. TECNOLOGÍAS UTILIZADAS

| Categoría | Tecnología | Versión |
|---|---|---|
| Framework | ASP.NET Core | .NET 10 (LTS) |
| ORM | Entity Framework Core | 10.0 |
| Base de Datos | PostgreSQL | 16+ |
| Documentación API | Swashbuckle.AspNetCore | Última |
| Autenticación | ASP.NET Core Identity | 10.0 |

---

## 5. ARQUITECTURA DE LA API

### 5.1 Convivencia MVC + API

La API REST convive con la capa MVC en el mismo proyecto:

| Capa | Ubicación | Retorna |
|---|---|---|
| MVC | `Controllers/` | Vistas HTML (Razor) |
| API REST | `Controllers/Api/` | JSON |

Ambas capas reutilizan `AppDbContext` y la misma base de datos PostgreSQL.

### 5.2 Diagrama de flujo

[Cliente HTTP] → [API REST] → [AppDbContext] → [PostgreSQL]
↓
[Entidades EF Core]

---

## 6. ENDPOINTS IMPLEMENTADOS

| Método | Ruta | Descripción | Respuesta |
|---|---|---|---|
| GET | `/api/prueba` | Endpoint de prueba de la API | `200 OK` + JSON |

### 6.1 Respuesta del endpoint `/api/prueba`

```json
{
  "status": "OK",
  "mensaje": "API REST de Agua Santa Clara funcionando correctamente",
  "fecha": "2026-09-26T16:21:10.9760932Z"
}

---

## 7. CONFIGURACIÓN DE SWAGGER

En `Program.cs` se agregó:

- `builder.Services.AddEndpointsApiExplorer()`
- `builder.Services.AddSwaggerGen()`
- `app.UseSwagger()`
- `app.UseSwaggerUI()` con ruta `/swagger`

Swagger solo se activa en ambiente de desarrollo (`app.Environment.IsDevelopment()`).

### 7.1 Código de configuración

```csharp
// API REST + Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "API Agua Santa Clara",
        Version = "v1",
        Description = "API REST del Sistema de Pedidos y Distribución"
    });
});
```

```csharp
// Swagger (solo en desarrollo)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Agua Santa Clara v1");
        options.RoutePrefix = "swagger";
    });
}
```

---

## 8. ESTRUCTURA DEL PROYECTO API

### 8.1 Carpetas relevantes

```
AguaSantaClara.Web/
└── Controllers/
    ├── AccountController.cs
    ├── UsuariosController.cs
    ├── ProductosController.cs
    ├── InsumosController.cs
    ├── ClientesController.cs
    └── Api/                          ← Controllers de la API REST
        └── PruebaApiController.cs
```

### 8.2 Descripción

| Elemento | Propósito |
|---|---|
| `Controllers/` | Controllers MVC que devuelven vistas HTML |
| `Controllers/Api/` | Controllers API que devuelven JSON |
| `Program.cs` | Configuración de la API + Swagger |
| `AppDbContext` | Acceso a datos compartido entre MVC y API |

---

## 9. ENTREGABLES DE LA HT-02

| Entregable | Descripción |
|---|---|
| API REST configurada | Proyecto con API y MVC conviviendo |
| Swagger | Documentación automática en `/swagger` |
| Endpoint de prueba | `GET /api/prueba` funcionando |
| Configuración en `Program.cs` | Swagger + ApiExplorer registrados |

---

## 10. CONCLUSIÓN

La Historia Técnica HT-02 se completó satisfactoriamente:

- La API REST está configurada sobre el proyecto MVC existente.
- Swagger está expuesto en `/swagger` para documentación automática.
- El endpoint de prueba responde correctamente con `200 OK`.
- La estructura permite agregar nuevos endpoints API sin afectar la capa MVC.
- Se mantiene la arquitectura base (MVC + Services + Repositories) definida en HT-03.

---

**Fin del documento.**