using UnityEngine;

/// <summary>
/// Drop on any trigger volume to cut, restore or toggle the hotel's power when the player walks in
/// (uncheck 'Once' to let it fire every time). The Cut / Restore / Toggle methods can also be hooked to a
/// UnityEvent or called from another script. Nothing else is needed: HotelPower does the rest.
/// </summary>
public class PowerTrigger : MonoBehaviour
{
    public enum Effect { Cut, Restore, Toggle }

    [SerializeField] private Effect onEnter = Effect.Cut;
    [SerializeField] private bool once = true;
    [Tooltip("A cut does nothing while the player has no flashlight.")]
    [SerializeField] private bool needFlashlight = true;

    private bool used;

    public void Cut() { HotelPower.Cut(needFlashlight); }
    public void Restore() { HotelPower.Restore(); }
    public void Toggle() { HotelPower.Toggle(needFlashlight); }

    private void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag("Player")) return;
        if (once) used = true;
        if (onEnter == Effect.Cut) Cut(); else if (onEnter == Effect.Restore) Restore(); else Toggle();
    }
}
