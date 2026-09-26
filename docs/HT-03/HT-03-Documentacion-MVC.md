# HT-03: Estructuración del Patrón MVC y Frontend Base

**Sistema de Pedidos y Distribución — Agua Santa Clara S.A.C.**

---

## CONTROL DE VERSIONES

| Versión | Hecha por | Revisada por | Aprobada por | Fecha | Motivo |
|---|---|---|---|---|---|
| 1.0 | Equipo de Desarrollo | Líder Técnico | Product Owner | 21/09/2026 | Creación del documento |

---

## INFORMACIÓN DEL PROYECTO

| Campo | Detalle |
|---|---|
| **Código del Proyecto** | IS2-SPD |
| **Nombre del Proyecto** | Sistema de Pedidos y Distribución |
| **Empresa** | Agua Santa Clara S.A.C. |
| **Product Owner** | Fernández Rodríguez, José Sebastián |
| **Scrum Master** | [Nombre del Scrum Master] |
| **Historia Técnica** | HT-03 — Estructuración del Patrón MVC y Frontend Base |
| **Versión del Documento** | 1.0 |
| **Fecha de Emisión** | 21/09/2026 |

---

## 1. INTRODUCCIÓN

El presente documento describe la estructura del proyecto, las convenciones de nomenclatura y los estándares adoptados en el marco de la Historia Técnica HT-03: Estructuración del Patrón MVC y Frontend Base (ASP.NET Core).

Su propósito es servir como guía de referencia para todo el equipo de desarrollo, garantizando consistencia, mantenibilidad y escalabilidad a lo largo de los sprints del proyecto.

---

## 2. OBJETIVOS

- Definir la estructura de carpetas del proyecto siguiendo el patrón Modelo-Vista-Controlador (MVC).
- Establecer las convenciones de nomenclatura para clases, variables y archivos.
- Documentar las capas de la arquitectura y sus responsabilidades.
- Servir como referencia técnica para nuevos integrantes del equipo.
- Garantizar la consistencia del código a lo largo de todos los sprints.

---

## 3. ALCANCE DE LA HT-03

La presente Historia Técnica comprende las siguientes tareas:

| Código | Tarea |
|---|---|
| T32 | Estructurar carpetas MVC |
| T33 | Configurar rutas y controllers |
| T34 | Crear Repository/Service |
| T35 | Configurar Entity Framework |
| T36 | Crear layout base Bootstrap |
| T37 | Registrar servicios con DI |
| T38 | Validar conexión con EF Core |
| T39 | Documentar estructura MVC |

> **Nota:** La definición detallada de la base de datos (tablas, constraints, normalización) corresponde a la HT-04, documentada por separado.

---

## 4. TECNOLOGÍAS UTILIZADAS

| Categoría | Tecnología | Versión |
|---|---|---|
| Framework | ASP.NET Core | .NET 10 (LTS) |
| Lenguaje | C# | 14.0 |
| ORM | Entity Framework Core | 10.0 |
| Base de Datos | PostgreSQL | 16+ |
| Frontend | Bootstrap | 5.x |
| Iconografía | Bootstrap Icons | 1.11.x |
| Patrón de diseño | MVC + Repository + Service | — |
| Control de Versiones | Git + GitHub | Última |
| IDE Recomendado | Visual Studio 2022 / VS Code | Última |

---

## 5. ARQUITECTURA DEL PROYECTO

El proyecto sigue el patrón MVC (Modelo-Vista-Controlador) complementado con las capas Repository y Service, para separar responsabilidades y facilitar el mantenimiento.

### 5.1 Diagrama de capas

![Arquitectura del Proyecto](arquitectura.png)

> Diagrama generado en Mermaid. Ver archivo `arquitectura.mmd` para el código fuente.

### 5.2 Responsabilidades por capa

| Capa | Responsabilidad |
|---|---|
| **Views** | Presentación visual al usuario (Razor + Bootstrap). |
| **Controllers** | Recibir peticiones HTTP, coordinar lógica y devolver respuestas. |
| **Services** | Aplicar reglas de negocio, validaciones y orquestar operaciones. |
| **Repositories** | Encapsular el acceso a la base de datos. |
| **AppDbContext** | Mapear entidades a tablas PostgreSQL mediante EF Core. |
| **PostgreSQL** | Almacenar los datos del sistema. |

---

## 6. ESTRUCTURA DE CARPETAS

### 6.1 Vista general

![Estructura de Carpetas](estructura-carpetas.png)

### 6.2 Detalle completo

AguaSantaClara/
│
├── AguaSantaClara.slnx ← Solución del proyecto
│
├── docs/ ← Documentación
│ ├── HT-03/
│ └── HT-04/
│
├── db/ ← Scripts SQL de referencia
│ └── scripts/
│
└── AguaSantaClara.Web/ ← Proyecto MVC
│
├── Controllers/ ← Controladores MVC
│ └── Api/ ← Endpoints API REST
│
├── Models/ ← Modelos del dominio
│ ├── Entities/ ← Entidades EF Core
│ └── Configurations/ ← Configuraciones IEntityTypeConfiguration<T>
│
├── Data/ ← Contexto de datos
│ └── AppDbContext.cs ← DbContext principal
│
├── Repositories/ ← Patrón Repository
│ ├── IGenericRepository.cs
│ └── GenericRepository.cs
│
├── Services/ ← Lógica de negocio
│ └── [servicios específicos]
│
├── Views/ ← Vistas Razor
│ ├── Shared/
│ │ ├── _Layout.cshtml ← Layout base
│ │ └── _ViewStart.cshtml
│ └── [vistas por módulo]
│
├── Migrations/ ← Migraciones EF Core
│
├── wwwroot/ ← Archivos estáticos
│ ├── css/
│ ├── js/
│ └── lib/
│
├── Program.cs ← Configuración DI
├── appsettings.json ← Configuración
└── AguaSantaClara.Web.csproj ← Archivo del proyecto


### 6.3 Descripción de carpetas

| Carpeta | Propósito |
|---|---|
| `Controllers/` | Controladores MVC que gestionan las peticiones HTTP. |
| `Controllers/Api/` | Controladores que exponen endpoints REST (JSON). |
| `Models/Entities/` | Clases que representan las tablas de la base de datos. |
| `Models/Configurations/` | Configuraciones de EF Core para cada entidad. |
| `Data/` | `AppDbContext` (contexto de datos). |
| `Repositories/` | Acceso a datos mediante patrón Repository. |
| `Services/` | Lógica de negocio y validaciones. |
| `Views/` | Vistas Razor que se renderizan al usuario. |
| `Migrations/` | Migraciones generadas por EF Core. |
| `wwwroot/` | Recursos estáticos (CSS, JS, imágenes, librerías). |

---

## 7. CONVENCIONES DE NOMENCLATURA

### 7.1 Código C#

| Elemento | Convención | Ejemplo |
|---|---|---|
| Clases | PascalCase | `Producto`, `UsuarioService` |
| Interfaces | PascalCase con prefijo `I` | `IProductoRepository` |
| Métodos | PascalCase | `ObtenerPorIdAsync()` |
| Propiedades | PascalCase | `PrecioVenta`, `FechaCreacion` |
| Variables locales | camelCase | `productoActual`, `totalRegistros` |
| Parámetros | camelCase | `nombreProducto` |
| Constantes | MAYÚSCULAS_CON_GUIONES | `MAX_INTENTOS_LOGIN` |
| Campos privados | `_camelCase` | `_context`, `_repository` |

### 7.2 Archivos y vistas

| Elemento | Convención | Ejemplo |
|---|---|---|
| Controladores | PascalCase + `Controller` | `ProductosController.cs` |
| Vistas | PascalCase | `Index.cshtml`, `Create.cshtml` |
| Carpetas de vistas | PascalCase | `Views/Productos/` |
| Servicios | PascalCase + `Service` | `ProductoService.cs` |
| Repositorios | PascalCase + `Repository` | `ProductoRepository.cs` |

### 7.3 Commits (Git)

| Prefijo | Uso | Ejemplo |
|---|---|---|
| `TXX:` | Tarea del Sprint | `T32: Estructurar carpetas MVC` |
| `fix:` | Corrección | `fix: corregir validación de precio` |
| `docs:` | Documentación | `docs: agregar manual de instalación` |
| `refactor:` | Reestructuración | `refactor: simplificar ProductoService` |

---

## 8. FRONTEND BASE (BOOTSTRAP)

### 8.1 Componentes utilizados

| Componente | Uso |
|---|---|
| Navbar | Barra de navegación superior |
| Card | Contenedor del contenido principal |
| Table | Listados de datos |
| Form | Formularios de creación/edición |
| Button | Acciones |
| Badge | Indicadores de estado |
| Breadcrumb | Navegación jerárquica |
| Dropdown | Menú de usuario |
| Alert | Mensajes al usuario |

### 8.2 Layout base (`_Layout.cshtml`)

El layout base incluye:

- **Navbar oscura** con las secciones: Inicio, Usuarios, Productos, Insumos, Clientes.
- **Menú de usuario** con opciones de Perfil, Configuración y Cerrar sesión.
- **Encabezado de página** con título, subtítulo y breadcrumb.
- **Contenedor principal** con el contenido de cada vista dentro de una tarjeta.
- **Footer** con año dinámico y versión del sistema.

### 8.3 Estilos personalizados (`site.css`)

El archivo `wwwroot/css/site.css` contiene:

- Tipografía base (Segoe UI / Roboto).
- Ajustes de Navbar, cards, tablas, formularios y botones.
- Media queries para responsive en móviles.

---

## 9. ENTREGABLES DE LA HT-03

| Entregable | Descripción |
|---|---|
| Proyecto MVC estructurado | Carpetas organizadas según patrón MVC |
| Controllers base | Controladores de los módulos principales |
| Repositorios y Services | Patrón Repository/Service implementado |
| EF Core configurado | Conexión a PostgreSQL funcionando |
| Layout con Bootstrap | Frontend base responsive |
| Documentación | Este documento |

---

**Fin del documento.**