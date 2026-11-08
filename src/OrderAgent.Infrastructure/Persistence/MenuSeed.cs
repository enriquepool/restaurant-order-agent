using OrderAgent.Domain.Menu;

namespace OrderAgent.Infrastructure.Persistence;

/// <summary>Menú de ejemplo para pruebas locales. Se inserta con la migración inicial.</summary>
internal static class MenuSeed
{
    public static readonly MenuItem[] Items =
    [
        new(1, "Tacos al pastor", "Orden de 3 tacos con piña, cebolla y cilantro", "Tacos", 75m),
        new(2, "Tacos de suadero", "Orden de 3 tacos", "Tacos", 75m),
        new(3, "Tacos de bistec", "Orden de 3 tacos", "Tacos", 80m),
        new(4, "Quesadilla de queso", "Tortilla de maíz hecha a mano", "Antojitos", 45m),
        new(5, "Gringa", "Tortilla de harina con pastor y queso", "Antojitos", 70m),
        new(6, "Enchiladas verdes", "3 enchiladas de pollo con crema y queso", "Platos fuertes", 120m),
        new(7, "Pozole rojo", "Tazón grande de pozole de cerdo", "Platos fuertes", 130m),
        new(8, "Agua de horchata", "Vaso de 500 ml", "Bebidas", 30m),
        new(9, "Agua de jamaica", "Vaso de 500 ml", "Bebidas", 30m),
        new(10, "Refresco", "Lata de 355 ml", "Bebidas", 28m),
        new(11, "Flan napolitano", "Rebanada", "Postres", 45m),
        new(12, "Churros", "3 piezas con azúcar y canela", "Postres", 40m),
    ];
}
