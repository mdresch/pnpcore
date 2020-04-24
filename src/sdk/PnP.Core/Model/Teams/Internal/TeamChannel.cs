﻿using Microsoft.Extensions.Logging;
using System.Dynamic;
using System.Text.Json;

namespace PnP.Core.Model.Teams
{
    [GraphType(GraphId = "id", GraphUri = V)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2243:Attribute string literals should parse correctly", Justification = "<Pending>")]
    internal partial class TeamChannel
    {
        private const string baseUri = "teams/{Parent.GraphId}/channels";
        private const string V = baseUri + "/{GraphId}";

        internal TeamChannel()
        {
            MappingHandler = (FromJson input) =>
            {
                switch (input.TargetType.Name)
                {
                    case "TeamChannelMembershipType": return JsonMappingHelper.ToEnum<TeamChannelMembershipType>(input.JsonElement);
                }

                input.Log.LogWarning($"Field {input.FieldName} could not be mapped when converting from JSON");

                return null;
            };

            // Handler to construct the Add request for this channel
            AddApiCallHandler = () =>
            {
                // Define the JSON body of the update request based on the actual changes
                dynamic body = new ExpandoObject();
                body.displayName = DisplayName;
                if (!string.IsNullOrEmpty(Description))
                {
                    body.description = Description;
                }

                // Serialize object to json
                var bodyContent = JsonSerializer.Serialize(body, typeof(ExpandoObject), new JsonSerializerOptions { WriteIndented = false });

                return new ApiCall(ApiHelper.ParseApiRequest(this, baseUri), ApiType.Graph, bodyContent);
            };

            // Validation handler to prevent updating the general channel
            ValidateUpdateHandler = (ref PropertyUpdateRequest propertyUpdateRequest) =>
            {
                // Prevent setting all values on the general channel
                if (DisplayName == "General")
                {
                    propertyUpdateRequest.CancelUpdate("Updating the general channel is not allowed.");
                }
            };

            UpdateApiCallOverrideHandler = (ApiCallRequest apiCallRequest) =>
            {
                if (DisplayName == "General")
                {
                    apiCallRequest.CancelRequest("Updating the general channel is not allowed.");
                }

                return apiCallRequest;
            };

            // Check delete, block when needed 
            DeleteApiCallOverrideHandler = (ApiCallRequest apiCallRequest) =>
            {
                if (DisplayName == "General")
                {
                    apiCallRequest.CancelRequest("Deleting the general channel is not allowed.");
                }

                return apiCallRequest;
            };

        }
    }
}