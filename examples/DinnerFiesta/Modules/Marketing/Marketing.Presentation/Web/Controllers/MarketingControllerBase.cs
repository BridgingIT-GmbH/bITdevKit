//----------------------
// <auto-generated>
//     Generated using the NSwag toolchain v14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0)) (http://NSwag.org)
// </auto-generated>
//----------------------

#pragma warning disable 108 // Disable "CS0108 '{derivedDto}.ToJson()' hides inherited member '{dtoBase}.ToJson()'. Use the new keyword if hiding was intended."
#pragma warning disable 114 // Disable "CS0114 '{derivedDto}.RaisePropertyChanged(String)' hides inherited member 'dtoBase.RaisePropertyChanged(String)'. To make the current member override that implementation, add the override keyword. Otherwise add the new keyword."
#pragma warning disable 472 // Disable "CS0472 The result of the expression is always 'false' since a value of type 'Int32' is never equal to 'null' of type 'Int32?'
#pragma warning disable 612 // Disable "CS0612 '...' is obsolete"
#pragma warning disable 649 // Disable "CS0649 Field is never assigned to, and will always have its default value null"
#pragma warning disable 1573 // Disable "CS1573 Parameter '...' has no matching param tag in the XML comment for ...
#pragma warning disable 1591 // Disable "CS1591 Missing XML comment for publicly visible type or member ..."
#pragma warning disable 8073 // Disable "CS8073 The result of the expression is always 'false' since a value of type 'T' is never equal to 'null' of type 'T?'"
#pragma warning disable 3016 // Disable "CS3016 Arrays as attribute arguments is not CLS-compliant"
#pragma warning disable 8600 // Disable "CS8600 Converting null literal or possible null value to non-nullable type"
#pragma warning disable 8602 // Disable "CS8602 Dereference of a possibly null reference"
#pragma warning disable 8603 // Disable "CS8603 Possible null reference return"
#pragma warning disable 8604 // Disable "CS8604 Possible null reference argument for parameter"
#pragma warning disable 8625 // Disable "CS8625 Cannot convert null literal to non-nullable reference type"
#pragma warning disable 8765 // Disable "CS8765 Nullability of type of parameter doesn't match overridden member (possibly because of nullability attributes)."

namespace BridgingIT.DevKit.Examples.DinnerFiesta.Modules.Marketing.Presentation.Web.Controllers
{
    using System = global::System;

    [System.CodeDom.Compiler.GeneratedCode("NSwag", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]

    public abstract class MarketingControllerBase : Microsoft.AspNetCore.Mvc.Controller
    {
        /// <summary>
        /// Gets an echo
        /// </summary>
        /// <returns>Resource request was successful.</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("api/marketing/echo", Name = "Marketing_Echo-Get")]
        public abstract System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<ResultResponseModel>> EchoGet(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        /// <param name="customerId">Customer Id</param>
        /// <returns>Resource request was successful.</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("api/marketing/customers/{customerId}", Name = "Marketing_CustomerFindOne")]
        public abstract System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<ResultOfCustomerResponseModel>> CustomerFindOne(string customerId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        /// <param name="customerId">Customer Id</param>
        /// <returns>Resource request was successful.</returns>
        [Microsoft.AspNetCore.Mvc.HttpPost, Microsoft.AspNetCore.Mvc.Route("api/marketing/customers/{customerId}/unsubscribe", Name = "Marketing_CustomerEmailUnsubscribe")]
        public abstract System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<ResultResponseModel>> CustomerEmailUnsubscribe(string customerId, System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

        /// <returns>Resource request was successful.</returns>
        [Microsoft.AspNetCore.Mvc.HttpGet, Microsoft.AspNetCore.Mvc.Route("api/marketing/customers", Name = "Marketing_CustomerFindAll")]
        public abstract System.Threading.Tasks.Task<Microsoft.AspNetCore.Mvc.ActionResult<ResultOfCustomersResponseModel>> CustomerFindAll(System.Threading.CancellationToken cancellationToken = default(System.Threading.CancellationToken));

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ResultResponseModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("messages")]
        public System.Collections.Generic.IEnumerable<string> Messages { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("isSuccess")]
        public bool IsSuccess { get; set; }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ProblemDetailsModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("type")]
        public string Type { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("status")]
        public int? Status { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("detail")]
        public string Detail { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("instance")]
        public string Instance { get; set; }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ValidationProblemDetailsModel : ProblemDetailsModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("errors")]
        [System.ComponentModel.DataAnnotations.Required]
        public System.Collections.Generic.IDictionary<string, System.Collections.Generic.IEnumerable<string>> Errors { get; set; } = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>>();

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ResultOfCustomerResponseModel : ResultResponseModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public CustomerResponseModel Value { get; set; }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class ResultOfCustomersResponseModel : ResultResponseModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public System.Collections.Generic.IEnumerable<CustomerResponseModel> Value { get; set; }

    }

    [System.CodeDom.Compiler.GeneratedCode("NJsonSchema", "14.4.0.0 (NJsonSchema v11.3.2.0 (Newtonsoft.Json v13.0.0.0))")]
    public partial class CustomerResponseModel
    {

        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("lastName")]
        public string LastName { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("email")]
        public string Email { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("emailOptOut")]
        public bool EmailOptOut { get; set; }

        private System.Collections.Generic.IDictionary<string, object> _additionalProperties;

        [System.Text.Json.Serialization.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties
        {
            get { return _additionalProperties ?? (_additionalProperties = new System.Collections.Generic.Dictionary<string, object>()); }
            set { _additionalProperties = value; }
        }

    }


}

#pragma warning restore  108
#pragma warning restore  114
#pragma warning restore  472
#pragma warning restore  612
#pragma warning restore 1573
#pragma warning restore 1591
#pragma warning restore 8073
#pragma warning restore 3016
#pragma warning restore 8600
#pragma warning restore 8602
#pragma warning restore 8603
#pragma warning restore 8604
#pragma warning restore 8625