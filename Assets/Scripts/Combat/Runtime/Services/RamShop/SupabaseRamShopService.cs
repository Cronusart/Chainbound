using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class SupabaseRamShopService : IRamShopService
    {
        private readonly SupabaseConfigSO config;

        public SupabaseRamShopService(SupabaseConfigSO config)
        {
            this.config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        public async Task<RamCatalog> GetCatalogAsync()
        {
            CatalogEnvelope response = await Post<CatalogEnvelope>("ram_shop_catalog", "{}");
            var catalog = new RamCatalog
            {
                checkoutAvailable = response.checkout_available,
                maintenanceReason = response.maintenance_reason,
                catalogVersion = response.catalog_version,
                cluster = response.cluster,
                authoritativeBalance = response.authoritative_balance,
                ledgerStatus = response.ledger_status,
                tenders = response.tenders ?? new List<string>(),
            };
            foreach (BundleEnvelope bundle in response.bundles ?? new List<BundleEnvelope>())
            {
                var mapped = new RamBundle
                {
                    sku = bundle.sku,
                    displayName = bundle.display_name,
                    ramAmount = bundle.ram_amount,
                    approvedBadge = bundle.approved_badge,
                };
                foreach (TenderEnvelope price in bundle.prices ?? new List<TenderEnvelope>())
                {
                    mapped.prices.Add(new RamTender
                    {
                        tender = price.tender,
                        atomicAmount = price.atomic_amount,
                        mint = price.mint,
                        decimals = price.decimals,
                    });
                }
                catalog.bundles.Add(mapped);
            }
            return catalog;
        }

        public Task<RamPurchaseOrder> PrepareAsync(string sku, string tender)
        {
            return PostOrder("ram_shop_prepare", JsonUtility.ToJson(new PrepareRequest { sku = sku, tender = tender }));
        }

        public Task<RamPurchaseOrder> ConfirmAsync(string orderId, string signature)
        {
            return PostOrder("ram_shop_confirm", JsonUtility.ToJson(new ConfirmRequest { order_id = orderId, signature = signature }));
        }

        public Task<RamPurchaseOrder> GetStatusAsync(string orderId, string signature = null, string reference = null)
        {
            return PostOrder("ram_shop_status", JsonUtility.ToJson(new StatusRequest
            {
                order_id = orderId,
                signature = signature,
                reference = reference,
            }));
        }

        private async Task<RamPurchaseOrder> PostOrder(string functionName, string body)
        {
            OrderEnvelope response = await Post<OrderEnvelope>(functionName, body);
            return new RamPurchaseOrder
            {
                orderId = response.order_id,
                state = ParseState(response.state),
                sku = response.sku,
                bundleName = response.bundle_name,
                ramAmount = response.ram_amount,
                transactionBytes = string.IsNullOrWhiteSpace(response.unsigned_transaction_base64)
                    ? Array.Empty<byte>()
                    : Convert.FromBase64String(response.unsigned_transaction_base64),
                messageHash = response.message_hash,
                quoteExpiresAt = response.quote_expires_at,
                paymentSignature = response.payment_signature,
                creditSignature = response.credit_signature,
                creditReceiptAddress = response.credit_receipt_address,
                balanceAfter = response.balance_after,
                payment = new RamPaymentSummary
                {
                    cluster = response.cluster,
                    destination = response.destination,
                    mint = response.mint,
                    tender = response.tender,
                    atomicAmount = response.payment_atomic_amount,
                    decimals = response.decimals,
                    feePayer = response.wallet,
                    estimatedFeeLamports = response.estimated_fee_lamports,
                    reference = response.reference,
                    programId = response.program_id,
                    paymentProgram = response.payment_program,
                },
            };
        }

        private async Task<T> Post<T>(string functionName, string json) where T : ResponseEnvelope
        {
            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(config, functionName, json);
            T payload = string.IsNullOrWhiteSpace(response.body) ? null : JsonUtility.FromJson<T>(response.body);
            if (!response.IsSuccess || payload == null || !payload.success)
                throw new RamShopServiceException(payload?.error ?? "ram_shop_request_failed", response.statusCode);
            return payload;
        }

        private static RamPurchaseState ParseState(string state)
        {
            switch ((state ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "quoted": return RamPurchaseState.Quoted;
                case "submitted": return RamPurchaseState.Submitted;
                case "payment_verified": return RamPurchaseState.PaymentVerified;
                case "credit_submitted": return RamPurchaseState.CreditSubmitted;
                case "settled": return RamPurchaseState.Settled;
                case "expired_unpaid": return RamPurchaseState.ExpiredUnpaid;
                case "rejected": return RamPurchaseState.Rejected;
                case "failed": return RamPurchaseState.Failed;
                case "manual_review": return RamPurchaseState.ManualReview;
                default: return RamPurchaseState.Unknown;
            }
        }

        [Serializable] private class ResponseEnvelope { public bool success; public string error; }
        [Serializable] private sealed class PrepareRequest { public string sku; public string tender; }
        [Serializable] private sealed class ConfirmRequest { public string order_id; public string signature; }
        [Serializable] private sealed class StatusRequest { public string order_id; public string signature; public string reference; }

        [Serializable]
        private sealed class CatalogEnvelope : ResponseEnvelope
        {
            public bool checkout_available;
            public string maintenance_reason;
            public string catalog_version;
            public string cluster;
            public string authoritative_balance;
            public string ledger_status;
            public List<BundleEnvelope> bundles;
            public List<string> tenders;
        }

        [Serializable]
        private sealed class BundleEnvelope
        {
            public string sku;
            public string display_name;
            public string ram_amount;
            public string approved_badge;
            public List<TenderEnvelope> prices;
        }

        [Serializable]
        private sealed class TenderEnvelope
        {
            public string tender;
            public string atomic_amount;
            public string mint;
            public int decimals;
        }

        [Serializable]
        private sealed class OrderEnvelope : ResponseEnvelope
        {
            public string order_id;
            public string state;
            public string cluster;
            public string wallet;
            public string sku;
            public string bundle_name;
            public string ram_amount;
            public string tender;
            public string payment_atomic_amount;
            public string mint;
            public int decimals;
            public string destination;
            public string reference;
            public string program_id;
            public string payment_program;
            public string unsigned_transaction_base64;
            public string message_hash;
            public string estimated_fee_lamports;
            public string quote_expires_at;
            public string payment_signature;
            public string credit_signature;
            public string credit_receipt_address;
            public string balance_after;
        }
    }
}
