namespace FsDynamics.Crm

open Microsoft.Xrm.Sdk
open Microsoft.Crm.Sdk.Messages
open Microsoft.Xrm.Sdk.Client
open System

type internal CrmConnection(connectionString:string) =
    let parts = 
        connectionString.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s1 -> 
            let s2 = s1.Split([|'='|], 2)
            (Array.head s2, Array.last s2))
        |> Map.ofArray

    let rec findPart partNames = 
        if List.isEmpty partNames
        then None
        else
            match List.head partNames |> parts.TryFind with
            | Some value -> Some value
            | None -> List.tail partNames |> findPart

    member this.CreateServiceUri () =
        match findPart ["ServiceUri"; "Service Uri"] with
        | Some uri -> System.Uri uri
        | None ->
            match findPart ["Url"; "Server"; "ServerUrl"] with
            | Some url -> url.TrimEnd('/') |> sprintf "%s/XrmServices/2011/Organization.svc" |> System.Uri
            | None -> failwith "ServiceUri or Url must be provided."

    member this.CreateCredential () =
        let username = findPart ["UserName"; "Username"; "User"; "UserId"; "User Id"]
        let password = findPart ["Password"]
        let domain = findPart ["Domain"]
        let creds = AuthenticationCredentials ()

        match (domain, username, password) with
        | (_, None, None) ->
            creds.ClientCredentials.Windows.ClientCredential <- System.Net.CredentialCache.DefaultNetworkCredentials
        | (None, Some u, Some p) ->
            creds.ClientCredentials.UserName.UserName <- u
            creds.ClientCredentials.UserName.Password <- p
        | (Some d, Some u, Some p) ->
            creds.ClientCredentials.UserName.UserName <- sprintf "%s\\%s" d u
            creds.ClientCredentials.UserName.Password <- p
        | _ -> failwith "Both UserName and Password must be specified in connection string."

        creds

type OrganizationServiceManager(connectionString:string) = 
    let connection = CrmConnection connectionString
    let serviceManagement = 
        ServiceConfigurationFactory.CreateManagement<IOrganizationService>(connection.CreateServiceUri ())
    let mutable tokenResponse : SecurityTokenResponse option = None

    let (|ExpiredToken|ValidToken|) (token: SecurityTokenResponse) =
        if System.DateTime.UtcNow > token.Token.ValidTo.AddMinutes(float -15)
        then ExpiredToken
        else ValidToken

    member this.GetService () : IOrganizationService =
        let getNewToken = fun () -> 
            let creds = connection.CreateCredential () |> serviceManagement.Authenticate
            tokenResponse <- Some creds.SecurityTokenResponse
            tokenResponse

        match serviceManagement.AuthenticationType with
        | AuthenticationProviderType.ActiveDirectory ->
            let creds = connection.CreateCredential ()
            new OrganizationServiceProxy (serviceManagement, creds.ClientCredentials) :> IOrganizationService
        | _ ->
            let token =
                match tokenResponse with
                | Some tr -> match tr with ValidToken -> tokenResponse | ExpiredToken -> getNewToken ()
                | None -> getNewToken ()
            new OrganizationServiceProxy (serviceManagement, token.Value) :> IOrganizationService

module Entity =
    let getAttributeValue<'T when 'T : null> (logicalName: string) (e: Entity) =
        let value = e.GetAttributeValue<'T> logicalName
        match value with
        | null -> None
        | _ -> Some value
    
    let toEntityReference (e: Entity) =
        e.ToEntityReference ()

module Organization =
    open Microsoft.Xrm.Sdk.Query
    open System.Collections.Generic
    open System.Xml.Linq
    
    let whoAmI (svc: IOrganizationService) =
        try
            WhoAmIRequest () |> svc.Execute :?> WhoAmIResponse |> Ok
        with ex -> Error ex
    
    let retrieveMultiple (svc: IOrganizationService) (q:QueryBase) =
        try
            svc.RetrieveMultiple q |> Ok
        with ex -> Error ex

    let fetchMultiple (svc: IOrganizationService) fetch =
        FetchExpression fetch |> retrieveMultiple svc
    
    let internal addPagingAttr fetch page cookie =
        let (!!) s = XName.Get s
        let doc = XDocument.Parse fetch
        let fetchAttr = !!"fetch" |> doc.Element
        fetchAttr.SetAttributeValue (!!"page", page)
        fetchAttr.SetAttributeValue (!!"count", 5000)
        fetchAttr.SetAttributeValue (!!"paging-cookie", cookie)
        fetchAttr.Document.Root.ToString ()

    let rec internal fetchAllImpl results page cookie svc fetch =
        let fetch' = addPagingAttr fetch page cookie
        let r = fetchMultiple svc fetch'
        match r with
        | Ok ec -> 
            let es = Seq.append results ec.Entities
            match ec.MoreRecords with
            | false -> List es |> EntityCollection |> Ok
            | true -> fetchAllImpl es (page+1) ec.PagingCookie svc fetch'
        | Error ex -> Error ex

    let fetchAll svc fetch = fetchAllImpl Seq.empty<Entity> 1 "" svc fetch

    let executeMultipleAll () = ()
