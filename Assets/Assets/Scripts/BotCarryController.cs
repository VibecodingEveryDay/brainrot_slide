using UnityEngine;

/// <summary>
/// Простейшая заглушка для контроллера переноски брейнротов ботом.
/// Реализует тот же интерфейс, что и у игрока, чтобы существующий код компилировался.
/// </summary>
public class BotCarryController : MonoBehaviour, ICarryController
{
    [SerializeField] private Transform carrierTransformOverride;

    private BrainrotObject currentCarriedObject;

    public void CarryObject(BrainrotObject obj)
    {
        currentCarriedObject = obj;
    }

    public bool CanCarry()
    {
        return currentCarriedObject == null;
    }

    public BrainrotObject GetCurrentCarriedObject()
    {
        return currentCarriedObject;
    }

    public void DropObject()
    {
        currentCarriedObject = null;
    }

    public Transform GetCarrierTransform()
    {
        return carrierTransformOverride != null ? carrierTransformOverride : transform;
    }
}

