#r @"packages/Microsoft.CrmSdk.CoreAssemblies/lib/net452/Microsoft.Xrm.Sdk.dll"
#r @"packages/Microsoft.CrmSdk.CoreAssemblies/lib/net452/Microsoft.Crm.Sdk.Proxy.dll"
#r @"src/bin/Debug/FsDynamics.dll"

open FsDynamics.Crm
open Microsoft.Xrm.Sdk

// Define your CRM connection
let connectionString = "Url=https://mycrmorganization.crm.dynamics.com;User=ian.moore@example.com;Password=abc123;"
let orgServiceManager = OrganizationServiceManager connectionString

// Get an instance of IOrganizationService
let orgService = orgServiceManager.GetService ()
