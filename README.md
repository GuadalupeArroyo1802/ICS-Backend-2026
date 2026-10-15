# ICS TP3 - 2026
Estrategia de ramas

- `main`: versión estable (producción).
- `development`: integración del trabajo en curso.
- `feature/nombre-funcionalidad`: nuevas funcionalidades, salen de `development`.
- `hotfix/descripcion`: correcciones urgentes.

No se permite hacer commits directos sobre `main` ni `development`.








# DSW
# Trabajo Práctico Integrador
## Desarrollo de Software
### Backend
#### Integrantes
Arroyo Guadalupe - 56813 | Ala Rué Nicolás - 56220 | Amado Maximiliano - 56666

## Requisitos previos para el uso
- .NET 7 SDK o superior
- SQL Server (LocalDB o instancia)
- Visual Studio 2022 con soporte para ASP .NET y EF Core
- (Opcional) Postman, si se desea probar los endpoints

## Configuración Inicial
- Clonar el repositorio: https://github.com/GuadalupeArroyo1802/Dsw2025Tpi.git
- Abrir el archivo de solución: Con el archivo ya clonado, abrir la solución: _Dsw2025Tpi.sln_
- Configurar la cadena de conexión: Editar el archivo appsettings.json en el proyecto DSW2025TPI.Api: 
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=Dsw2025TpiDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
- Crear y aplicar las migraciones (En caso de usar EF Core): Al tener dos contextos dentro del mismo proyecto, al agregar las migraciones y hacer el update, se debe especificar el tipo de contexto. 
Para configurar las migraciones de la base de datos del proyecto:
     + *Ir a la opción *Herramientas* de la barra de tareas --> Seleccionar Administrador de paquetes NuGet --> Seleccionar Consola del Administrador de Paquetes
     + En la consola seleccionar el proyecto de DSW2025TPi.Data como proyecto predeterminado.
     + Ejecutar el comando: *Add-Migration Initial_Model -Context Dsw2025TpiContext*
     + Tras ver el mensaje Build succeded, ejecutar el comando *Update-Database -Context Dsw2025TpiContext*
 Para configurar las migraciones de la base de datos de seguridad
     + *Ir a la opción *Herramientas* de la barra de tareas --> Seleccionar Administrador de paquetes NuGet --> Seleccionar Consola del Administrador de Paquetes
     + En la consola seleccionar el proyecto de DSW2025TPi.Data como proyecto predeterminado.
     + Ejecutar el comando: *Add-Migration Authenticate_Model -Context AuthenticateContext*
     + Tras ver el mensaje Build succeded, ejecutar el comando *Update-Database -Context AuthenticateContext*
- Compilar el archivo: Presionar *Ctrl + Mayús. + B*, o hacer click en compilar solución
- Ejecutar la API: Presionar *Ctrl + F5*, o hacer click en iniciar sin depurar

## Endpoints utilizados
1) Crear un producto
POST /api/products
Crea un nuevo producto con los datos proporcionados, y lo almacena en la base de datos.
✔Caso de éxito: Devuelve el producto creado y código 201.
❌Caso de error: Devuelve 400 si los datos son inválidos.

2) Obtener todos los productos
GET /api/products
Devuelve todos los productos registrados en la base de datos.
✔ Caso de éxito: Devuelve la lista de productos y código 200.
❌Caso de error: Devuelve 204 si no hay productos.

3) Obtener un producto por ID
GET /api/products/{id}
Devuelve los detalles de un producto específico, que se encuentra en la base de datos.
✔Caso de éxito: Devuelve el producto solicitado y código 200.
❌Caso de error: Devuelve 404 si el producto no existe.

4) Actualizar un producto
PUT /api/products/{id}
Actualiza los datos de un producto existente en la base de datos.
✔Caso de éxito: Devuelve el producto actualizado y código 200.
❌Caso de error: Devuelve 400 si los datos son inválidos.
❌Caso de error: Devuelve 404 si el producto no existe.

5) Inhabilitar un producto
PATCH /api/products/{id}
Cambia el atributo IsActive del producto a false, inhabilitándolo para crear una orden con él incluido.
✔Caso de éxito: Devuelve código 204 si se inhabilita correctamente.
❌Caso de error: Devuelve 404 si el producto no existe.

6) Crear una orden
POST /api/orders
Registra una orden con customerId, direcciones y productos.
✔Caso de éxito: Devuelve la orden creada y código 201.
❌Caso de error: Devuelve 400 si los datos son inválidos o no hay stock suficiente.

7) Obtener todas las órdenes
GET /api/orders
Devuelve la lista paginada de órdenes, con 10 elementos máximo por página.
✔Caso de éxito: Devuelve órdenes y código 200.
❌Caso de error: Devuelve 500 si ocurre un error inesperado.

8) Obtener una orden por ID
GET /api/orders/{id}
Devuelve los detalles de una orden específica.
✔Caso de éxito: Devuelve la orden y código 200.
❌Caso de error: Devuelve 404 si la orden no existe.

9) Actualizar el estado de una orden
PUT /api/orders/{id}/status
Cambia el estado de una orden (ej. a Processing, Delivered).
✔Caso de éxito: Devuelve la orden con nuevo estado y código 200.
❌Caso de error: Devuelve 400 si el estado es inválido o la transición no es permitida.
❌Caso de error: Devuelve 404 si la orden no existe.
