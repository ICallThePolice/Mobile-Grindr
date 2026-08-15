namespace SpellSystem.Data
{
    public enum ShapeType
    {
        Triangle, // Вектор / Точечный импульс
        Circle,   // Сфера / Область (АоЕ)
        Square    // Защита / Бастион / На себя
    }

    public enum EnergyType
    {
        Vital, // Жизнь, физика, органика
        Psy,   // Разум, кинетика, пространство (ИСПРАВЛЕНО)
        Ereb   // Тьма, хаос, энтропия
    }
}