// CoddyKit (Unity) — RevenueCat customer-info listener (its own file: a MonoBehaviour added with AddComponent must be a
// top-level class whose file name matches). UNCOMPILED until the editor + the RC package are installed.
#if CODDY_RC
namespace CoddyKit.Bridges
{
    public sealed class RcCustomerInfoListener : Purchases.UpdatedCustomerInfoListener
    {
        internal RcPurchasesProvider Owner;
        public override void CustomerInfoReceived(Purchases.CustomerInfo info) => Owner?.Raise(info);
    }
}
#endif
