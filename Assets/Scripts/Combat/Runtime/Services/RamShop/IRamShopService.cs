using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public interface IRamShopService
    {
        Task<RamCatalog> GetCatalogAsync();
        Task<RamPurchaseOrder> PrepareAsync(string sku, string tender);
        Task<RamPurchaseOrder> ConfirmAsync(string orderId, string signature);
        Task<RamPurchaseOrder> GetStatusAsync(string orderId, string signature = null, string reference = null);
    }

    [Serializable]
    public sealed class RamCatalog
    {
        public bool checkoutAvailable;
        public string maintenanceReason;
        public string catalogVersion;
        public string cluster;
        public string authoritativeBalance;
        public string ledgerStatus;
        public List<RamBundle> bundles = new List<RamBundle>();
        public List<string> tenders = new List<string>();
    }

    [Serializable]
    public sealed class RamBundle
    {
        public string sku;
        public string displayName;
        public string ramAmount;
        public string approvedBadge;
        public List<RamTender> prices = new List<RamTender>();
    }

    [Serializable]
    public sealed class RamTender
    {
        public string tender;
        public string atomicAmount;
        public string mint;
        public int decimals;

        public string FormatAmount() => RamAtomicAmountFormatter.Format(atomicAmount, decimals);
    }

    [Serializable]
    public sealed class RamPaymentSummary
    {
        public string cluster;
        public string destination;
        public string mint;
        public string tender;
        public string atomicAmount;
        public int decimals;
        public string feePayer;
        public string estimatedFeeLamports;
        public string reference;
        public RpcSimulationResult simulation;
    }

    [Serializable]
    public sealed class RamPurchaseOrder
    {
        public string orderId;
        public RamPurchaseState state;
        public string sku;
        public string bundleName;
        public string ramAmount;
        public byte[] transactionBytes;
        public string messageHash;
        public string quoteExpiresAt;
        public string paymentSignature;
        public string creditSignature;
        public string creditReceiptAddress;
        public string balanceAfter;
        public RamPaymentSummary payment;
    }

    public enum RamPurchaseState
    {
        Unknown,
        Quoted,
        Submitted,
        PaymentVerified,
        CreditSubmitted,
        Settled,
        ExpiredUnpaid,
        Rejected,
        Failed,
        ManualReview,
    }

    public sealed class RamShopServiceException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }

        public RamShopServiceException(string errorCode, int statusCode)
            : base(errorCode)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
