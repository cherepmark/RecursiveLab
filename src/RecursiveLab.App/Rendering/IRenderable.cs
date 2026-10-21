using System.Drawing;

namespace RecursiveLab.App.Rendering;

/// <summary>
/// Общий контракт холстов: отрисовка в произвольный Graphics заданного размера.
/// Нужен самопроверке, чтобы сохранять изображения без показа окна.
/// </summary>
internal interface IRenderable
{
    void Render(Graphics graphics, Size size);
}
