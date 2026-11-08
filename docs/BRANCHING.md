# Estrategia de branches

El código se mantiene aislado por ambiente usando tres branches permanentes:

| Branch | Ambiente | Contenido |
|---|---|---|
| `develop` | Desarrollo | Integración de todos los cambios aprobados. Branch por defecto. |
| `stage` | Pruebas / QA | Versión candidata a producción. |
| `master` | Producción | Solo código liberado. |

## Flujo

```
feature/xxx ──PR──► develop ──PR──► stage ──PR──► master
fix/xxx     ──PR──┘
```

1. Todo cambio nace en una rama corta creada desde `develop`:
   - `feature/<descripcion>` para funcionalidad nueva.
   - `fix/<descripcion>` para correcciones.
   - `chore/<descripcion>` para mantenimiento (dependencias, configuración).
2. Se abre un **Pull Request hacia `develop`**, que el code reviewer revisa y aprueba.
3. Las promociones entre ambientes también son PRs:
   - `develop → stage` cuando un conjunto de cambios está listo para pruebas.
   - `stage → master` cuando pasa QA y se libera a producción.
4. Las ramas `feature/*`, `fix/*` y `chore/*` se eliminan automáticamente al hacer merge.

## Hotfixes en producción

Un error urgente en producción se corrige con una rama `hotfix/<descripcion>` creada desde `master`.
Se abre un PR hacia `master` y después el mismo cambio se lleva a `stage` y `develop`, para que
ningún ambiente quede atrás.

## Reglas de protección

`develop`, `stage` y `master` están protegidas en GitHub:

- No se permite push directo: todo entra por Pull Request.
- No se permite force push ni borrar la rama.
- Las reglas aplican también a los administradores.

## Mensajes de commit

Se usa [Conventional Commits](https://www.conventionalcommits.org/es/):

```
feat: agrega herramienta AddItem al agente
fix: corrige cálculo del total con modificadores
docs: documenta estrategia de branches
chore: actualiza paquetes NuGet
```
