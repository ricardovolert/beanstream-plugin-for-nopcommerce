using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Routing;
using Nop.Core.Domain.Orders;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Beanstream.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;

namespace Nop.Plugin.Payments.Beanstream
{
    /// <summary>
    /// Beanstream payment processor
    /// </summary>
    public class BeanstreamPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly BeanstreamPaymentSettings _beanstreamPaymentSettings;
        private readonly HttpContextBase _httpContext;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly ISettingService _settingService;

        #endregion

        #region Ctor

        public BeanstreamPaymentProcessor(BeanstreamPaymentSettings beanstreamPaymentSettings,
            HttpContextBase httpContext,
            IOrderTotalCalculationService orderTotalCalculationService,
            ISettingService settingService)
        {
            this._beanstreamPaymentSettings = beanstreamPaymentSettings;
            this._httpContext = httpContext;
            this._orderTotalCalculationService = orderTotalCalculationService;
            this._settingService = settingService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get Beanstream URL
        /// </summary>
        /// <returns>URL</returns>
        protected string GetBeanstreamUrl()
        {
            return "https://www.beanstream.com/scripts/payment/payment.asp";
        }

        /// <summary>
        /// Claculates MD5 hash
        /// </summary>
        /// <param name="input">Input string for the encoding</param>
        /// <returns>MD5 hash</returns>
        protected string CalculateMD5hash(string input)
        {
            var md5Hasher = new MD5CryptoServiceProvider();
            var hash = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

            var output = new StringBuilder();
            foreach (var character in hash)
            {
                output.Append(character.ToString("x2"));
            }

            return output.ToString();
        }
        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var builder = new StringBuilder();

            //common
            builder.AppendFormat("merchant_id={0}", HttpUtility.UrlEncode(_beanstreamPaymentSettings.MerchantId));

            //pass order
            builder.AppendFormat("&trnOrderNumber={0}", postProcessPaymentRequest.Order.Id);
            var orderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            builder.AppendFormat("&trnAmount={0}", orderTotal.ToString("0.00", CultureInfo.InvariantCulture));

            //address
            if (postProcessPaymentRequest.Order.BillingAddress != null)
            {
                builder.AppendFormat("&ordName={0}", HttpUtility.UrlEncode(string.Format("{0} {1}",
                    postProcessPaymentRequest.Order.BillingAddress.FirstName, postProcessPaymentRequest.Order.BillingAddress.LastName)));
                builder.AppendFormat("&ordEmailAddress={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Email));
                builder.AppendFormat("&ordPhoneNumber={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.PhoneNumber));
                builder.AppendFormat("&ordAddress1={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Address1));
                builder.AppendFormat("&ordAddress2={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Address2));
                builder.AppendFormat("&ordCity={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.City));
                builder.AppendFormat("&ordProvince={0}", postProcessPaymentRequest.Order.BillingAddress.StateProvince != null ?
                        HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.StateProvince.Abbreviation) : string.Empty);
                builder.AppendFormat("&ordCountry={0}", postProcessPaymentRequest.Order.BillingAddress.Country != null ?
                        HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.Country.TwoLetterIsoCode) : string.Empty);
                builder.AppendFormat("&ordPostalCode={0}", HttpUtility.UrlEncode(postProcessPaymentRequest.Order.BillingAddress.ZipPostalCode));
            }

            //creating hash value
            var hash = CalculateMD5hash(string.Format("{0}{1}", builder.ToString(), _beanstreamPaymentSettings.HashKey));
            builder.AppendFormat("&hashValue={0}", hash);

            //post
            _httpContext.Response.Redirect(string.Format("{0}?{1}", GetBeanstreamUrl(), builder));
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            var result = this.CalculateAdditionalFee(_orderTotalCalculationService, cart,
                _beanstreamPaymentSettings.AdditionalFee, _beanstreamPaymentSettings.AdditionalFeePercentage);
            return result;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            
            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentBeanstream";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Beanstream.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentBeanstream";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Beanstream.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Get type of the controller
        /// </summary>
        /// <returns>Controller type</returns>
        public Type GetControllerType()
        {
            return typeof(PaymentBeanstreamController);
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new BeanstreamPaymentSettings());

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.HashKey", "Hash key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.HashKey.Hint", "Specify hash key.");            
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.MerchantId", "Merchant Id");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.MerchantId.Hint", "Specify merchant Id.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Beanstream.Fields.RedirectionTip", "You will be redirected to Beanstream site to complete the order.");

            base.Install();
        }
        
        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<BeanstreamPaymentSettings>();

            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFee.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFeePercentage");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.AdditionalFeePercentage.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.HashValue");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.HashValue.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.MerchantId");
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.MerchantId.Hint");            
            this.DeletePluginLocaleResource("Plugins.Payments.Beanstream.Fields.RedirectionTip");

            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        #endregion
    }
}
