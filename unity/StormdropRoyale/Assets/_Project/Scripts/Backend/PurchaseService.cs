using System;
using System.Threading.Tasks;
using UnityEngine;
#if STORMDROP_IAP
using Newtonsoft.Json.Linq;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
#endif

namespace Stormdrop
{
    /// <summary>
    /// Diamond top-ups through Google Play Billing (via Unity IAP, define STORMDROP_IAP).
    /// Flow: buy → Unity IAP returns a receipt → backend verifies with Google and credits once →
    /// we confirm (consume) the purchase. Unconfirmed purchases are re-delivered on next launch,
    /// so a crash never loses Diamonds.
    /// </summary>
    public static class PurchaseService
    {
        public static readonly string[] DiamondProducts =
            { "diamonds_100", "diamonds_310", "diamonds_520", "diamonds_1060", "diamonds_2180", "diamonds_5600" };

        public static bool Ready { get; private set; }
        public static event Action<string> PurchaseCompleted;   // product id
        public static event Action<string> PurchaseFailed;      // message

#if STORMDROP_IAP
        static IStoreController _controller;

        class Listener : IDetailedStoreListener
        {
            public void OnInitialized(IStoreController controller, IExtensionProvider extensions) { _controller = controller; Ready = true; }
            public void OnInitializeFailed(InitializationFailureReason error) => Debug.LogWarning("IAP init failed: " + error);
            public void OnInitializeFailed(InitializationFailureReason error, string message) => Debug.LogWarning($"IAP init failed: {error} {message}");
            public void OnPurchaseFailed(Product product, PurchaseFailureReason reason) => PurchaseFailed?.Invoke(reason.ToString());
            public void OnPurchaseFailed(Product product, PurchaseFailureDescription d) => PurchaseFailed?.Invoke(d.message);

            public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
            {
                _ = VerifyAndConfirm(args.purchasedProduct);
                return PurchaseProcessingResult.Pending;   // confirmed after the backend credits Diamonds
            }
        }

        static async Task VerifyAndConfirm(Product p)
        {
            try
            {
                var receipt = JObject.Parse(p.receipt);
                var payload = JObject.Parse((string)receipt["Payload"]);
                var json = JObject.Parse((string)payload["json"]);
                string token = (string)json["purchaseToken"];
                await Backend.VerifyGooglePurchase(p.definition.id, token);
                _controller.ConfirmPendingPurchase(p);
                await Backend.RefreshProfile();
                PurchaseCompleted?.Invoke(p.definition.id);
                AnalyticsService.Log("purchase", "product", p.definition.id);
            }
            catch (Exception e)
            {
                // Left pending: Unity IAP will deliver it again next launch and we retry.
                PurchaseFailed?.Invoke("Purchase is being verified, Diamonds will arrive shortly. " + e.Message);
            }
        }
#endif

        public static void Initialize()
        {
#if STORMDROP_IAP
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var id in DiamondProducts) builder.AddProduct(id, ProductType.Consumable);
            UnityPurchasing.Initialize(new Listener(), builder);
#endif
        }

        public static string LocalizedPrice(string productId)
        {
#if STORMDROP_IAP
            var p = _controller?.products.WithID(productId);
            if (p != null) return p.metadata.localizedPriceString;
#endif
            return "—";
        }

        public static void Buy(string productId)
        {
#if STORMDROP_IAP
            if (!Ready) { PurchaseFailed?.Invoke("Store not ready yet"); return; }
            _controller.InitiatePurchase(productId);
#else
            PurchaseFailed?.Invoke("In-app purchases are not enabled in this build (add STORMDROP_IAP).");
#endif
        }
    }
}
