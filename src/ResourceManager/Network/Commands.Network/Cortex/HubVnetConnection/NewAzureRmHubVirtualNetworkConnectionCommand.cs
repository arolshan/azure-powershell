﻿namespace Microsoft.Azure.Commands.Network
{
    using AutoMapper;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Management.Automation;
    using System.Security;
    using Microsoft.Azure.Commands.Network.Models;
    using Microsoft.Azure.Commands.ResourceManager.Common.Tags;
    using Microsoft.Azure.Management.Network;
    using Microsoft.WindowsAzure.Commands.Common;
    using MNM = Microsoft.Azure.Management.Network.Models;
    using Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters;
    using System.Linq;
    using Microsoft.Azure.Management.Internal.Resources.Utilities.Models;

    [Cmdlet(VerbsCommon.New,
        "AzureRmHubVirtualNetworkConnection",
        DefaultParameterSetName = CortexParameterSetNames.ByVirtualHubName,
        SupportsShouldProcess = true),
        OutputType(typeof(PSHubVirtualNetworkConnection))]
    public class NewHubVirtualNetworkConnectionCommand : HubVnetConnectionBaseCmdlet
    {
        [Alias("ResourceName", "HubVirtualNetworkConnectionName")]
        [Parameter(
            Mandatory = true,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The resource name.")]
        [ValidateNotNullOrEmpty]
        public virtual string Name { get; set; }

        [Parameter(
            Mandatory = true,
            ParameterSetName = CortexParameterSetNames.ByVirtualHubName,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The resource group name.")]
        [ResourceGroupCompleter]
        [ValidateNotNullOrEmpty]
        public virtual string ResourceGroupName { get; set; }

        [Alias("VirtualHubName", "ParentVirtualHubName")]
        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = CortexParameterSetNames.ByVirtualHubName,
            HelpMessage = "The parent resource name.")]
        public string ParentResourceName { get; set; }

        [Alias("VirtualHub", "ParentVirtualHub")]
        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = CortexParameterSetNames.ByVirtualHubObject,
            HelpMessage = "The parent resource.")]
        public PSVirtualHub ParentResource { get; set; }

        [Alias("VirtualHubId", "ParentVirtualHubId")]
        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            ParameterSetName = CortexParameterSetNames.ByVirtualHubResourceId,
            HelpMessage = "The parent resource id.")]
        [ResourceIdCompleter("Microsoft.Network/virtualHubs")]
        public string ParentResourceId { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The remote virtual network to which this hub virtual network connection is connected.")]
        [ResourceGroupCompleter]
        public PSVirtualNetwork RemoteVirtualNetwork { get; set; }

        [Parameter(
            Mandatory = false,
            ValueFromPipelineByPropertyName = true,
            HelpMessage = "The remote virtual network id to which this hub virtual network connection is connected.")]
        [ResourceGroupCompleter]
        [ResourceIdCompleter("Microsoft.Network/virtualNetworks")]
        public string RemoteVirtualNetworkId { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Enable internet security for this connection.")]
        public bool? EnableInternetSecurity { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Run cmdlet in the background")]
        public SwitchParameter AsJob { get; set; }

        [Parameter(
            Mandatory = false,
            HelpMessage = "Do not ask for confirmation if you want to overrite a resource")]
        public SwitchParameter Force { get; set; }

        public override void Execute()
        {
            base.Execute();
            WriteWarning("The output object type of this cmdlet will be modified in a future release.");

            if (ParameterSetName.Equals(CortexParameterSetNames.ByVirtualHubObject, StringComparison.OrdinalIgnoreCase))
            {
                this.ResourceGroupName = this.ParentResource.ResourceGroupName;
                this.ParentResourceName = this.ParentResource.Name;
            }
            else if (ParameterSetName.Equals(CortexParameterSetNames.ByVirtualHubResourceId, StringComparison.OrdinalIgnoreCase))
            {
                var parsedResourceId = new ResourceIdentifier(this.ParentResourceId);
                this.ResourceGroupName = parsedResourceId.ResourceGroupName;
                this.ParentResourceName = parsedResourceId.ResourceName;
            }

            //// Get the virtual hub - this will throw not found if the resource does not exist
            PSVirtualHub parentVirtualHub = this.GetVirtualHub(this.ResourceGroupName, this.ParentResourceName);
            if (parentVirtualHub == null)
            {
                throw new PSArgumentException("The parent virtual hub mentioned could not be found.");
            }

            PSHubVirtualNetworkConnection hubVnetConnection = new PSHubVirtualNetworkConnection();
            hubVnetConnection.Name = this.Name;
            hubVnetConnection.EnableInternetSecurity = this.EnableInternetSecurity.HasValue ? this.EnableInternetSecurity.Value : false;

            //// Resolve the remote virtual network
            //// Let's not try to resolve this since this can be in other RG/Sub/Location
            if (this.RemoteVirtualNetwork != null)
            {
                hubVnetConnection.RemoteVirtualNetwork = new PSResourceId() { Id = this.RemoteVirtualNetwork.Id };
            }
            else if (!string.IsNullOrWhiteSpace(this.RemoteVirtualNetworkId))
            {
                hubVnetConnection.RemoteVirtualNetwork = new PSResourceId() { Id = this.RemoteVirtualNetworkId };
            }
            else
            {
                throw new PSArgumentException("A remote virtual network reference is required to create a HubVirtualNetworkConnection.");
            }

            if (parentVirtualHub.VirtualNetworkConnections == null)
            {
                parentVirtualHub.VirtualNetworkConnections = new List<PSHubVirtualNetworkConnection>();
            }

            parentVirtualHub.VirtualNetworkConnections.Add(hubVnetConnection);

            bool shouldProcess = this.Force.IsPresent;
            if (!shouldProcess)
            {
                shouldProcess = ShouldProcess(Name, Properties.Resources.CreatingResourceMessage);
            }

            if (shouldProcess)
            {
                this.CreateOrUpdateVirtualHub(this.ResourceGroupName, this.ParentResourceName, parentVirtualHub, parentVirtualHub.Tag);
                var createdVirtualHub = this.GetVirtualHub(this.ResourceGroupName, this.ParentResourceName);

                WriteObject(createdVirtualHub.VirtualNetworkConnections.FirstOrDefault(hubConnection => hubConnection.Name.Equals(this.Name, StringComparison.OrdinalIgnoreCase)));
            }
        }
    }
}
