﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters
{
    using Commands.Common.Authentication;
    using Commands.Common.Authentication.Abstractions;
    using Management.Internal.Resources;
    using Management.Internal.Resources.Models;
    using Properties;
    using Rest.Azure;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;

    /// <summary>
    /// This attribute will allow the user to autocomplete the -ResourceType parameter of a cmdlet with valid resource types
    /// </summary>
    public class ResourceTypeCompleterAttribute : PSCompleterBaseAttribute
    {
        private static IDictionary<int, IList<String>> _resourceTypesDictionary = new ConcurrentDictionary<int, IList<string>>();
        private static readonly object _lock = new object();
        public static int _timeout = 3;

        protected static IList<String> ResourceTypes
        {
            get
            {
                lock (_lock)
                {
                    IAzureContext context = AzureRmProfileProvider.Instance.Profile.DefaultContext;
                    var contextHash = HashContext(context);
                    if (!_resourceTypesDictionary.ContainsKey(contextHash))
                    {
                        var tempResourceTypeList = new List<string>();
                        try
                        {
                            var instance = AzureSession.Instance;
                            var client = instance.ClientFactory.CreateCustomArmClient<ResourceManagementClient>(
                                context.Environment.GetEndpointAsUri(AzureEnvironment.Endpoint.ResourceManager),
                                instance.AuthenticationFactory.GetServiceClientCredentials(context, AzureEnvironment.Endpoint.ResourceManager),
                                instance.ClientFactory.GetCustomHandlers());
                            client.SubscriptionId = context.Subscription.Id;
                            // Retrieve only the first page of resource types to display to the user
                            var resourceTypes = client.Providers.ListAsync();
                            if (resourceTypes.Wait(TimeSpan.FromSeconds(_timeout)))
                            {
                                tempResourceTypeList = CreateResourceTypeList(resourceTypes.Result);
                                if (resourceTypes.Result != null)
                                {
                                    _resourceTypesDictionary[contextHash] = tempResourceTypeList;
                                }
                            }
#if DEBUG
                            else
                            {
                                throw new InvalidOperationException("client.Providers.ListAsync() call timed out");
                            }
#endif
                        }

                        catch (Exception ex)
                        {
                            if (ex == null) { }
#if DEBUG
                            throw ex;
#endif
                        }

                        return tempResourceTypeList;
                    }

                    else
                    {
                        return _resourceTypesDictionary[contextHash];
                    }
                }
            }
        }

        /// <summary>
        /// This class will provide a list of resource groups that are available to the user (with default resource group first if it exists). This will then be available to the user to tab through.
        /// Example: [Parameter(ParameterSetName = ListByNameInTenantParameterSet, ValueFromPipelineByPropertyName = true, Mandatory = false), ResourceGroupCompleter()]
        /// </summary>
        /// <param name="resourceTypes"></param>
        public ResourceTypeCompleterAttribute()
        {
        }

        public override string[] GetCompleterValues()
        {
            return GetResourceTypes();
        }

        public static string[] GetResourceTypes(int timeout)
        {
            _timeout = timeout;
            return GetResourceTypes();
        }

        public static string[] GetResourceTypes()
        {
            return ResourceTypes.ToArray();
        }

        private static ScriptBlock CreateScriptBlock()
        {
            string script = "param($commandName, $parameterName, $wordToComplete, $commandAst, $fakeBoundParameter)\n" +
                "$locations = [Microsoft.Azure.Commands.ResourceManager.Common.ArgumentCompleters.ResourceTypeCompleterAttribute]::GetResourceTypes()\n" +
                "$locations | Where-Object { $_ -Like \"$wordToComplete*\" } | ForEach-Object { [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_) }";
            ScriptBlock scriptBlock = ScriptBlock.Create(script);
            return scriptBlock;
        }

        private static int HashContext(IAzureContext context)
        {
            return (context.Account.Id + context.Environment.Name + context.Subscription.Id + context.Tenant.Id).GetHashCode();
        }

        public static List<string> CreateResourceTypeList(IPage<Provider> result)
        {
            var tempResourceTypeList = new List<string>();
            if (result != null)
            {
                foreach (Provider provider in result)
                {
                    foreach (ProviderResourceType resourceType in provider.ResourceTypes)
                    {
                        var type = provider.NamespaceProperty + "/" + resourceType.ResourceType;
                        tempResourceTypeList.Add(type);
                    }
                }
            }
#if DEBUG
            else
            {
                throw new Exception("Result from client.ResourceGroups is null");
            }
#endif
            return tempResourceTypeList;
        }
    }
}