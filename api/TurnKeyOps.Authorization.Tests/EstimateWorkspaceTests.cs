using Azure;
using System.Text.Json;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using TurnKeyOps.Lib.Configurations;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Repositories.Interfaces;
using TurnKeyOps.Services;
using TurnKeyOps.Services.Interfaces;

namespace MedInsights.Authorization.Tests;
public sealed class EstimateWorkspaceTests
{
    [Fact] public void ConcreteQuantitiesAndTaxComeFromConfirmedInputsAndCatalog()
    {
        var doc=Document("concrete");doc.Inputs=new(){["length"]=Value(60),["width"]=Value(20),["depth"]=Value(4),["waste"]=Value(10)};
        doc.Options[0].Items[0].QuantityKey="cubicYards";
        var price=EstimatePricingEngine.Calculate(doc,Policy(),null,10);
        Assert.Empty(price.Blockers);var option=Assert.Single(price.Options);var line=Assert.Single(option.Lines);
        Assert.Equal(16.2963m,line.Quantity);Assert.Equal(1629.63m,option.Subtotal);Assert.Equal(162.96m,option.Discount);Assert.Equal(117.33m,option.Tax);Assert.Equal(1584m,option.Total);
    }
    [Theory] [InlineData("framing","studCount",11)] [InlineData("land-clearing","acreage",2)] [InlineData("doors-locks","openingCount",3)]
    public void TradeQuantitiesAreIsolated(string trade,string key,int expected)
    {
        var doc=Document(trade);doc.Inputs=new(){["length"]=Value(10),["height"]=Value(8),["spacing"]=Value(16),["openings"]=Value(2),["acreage"]=Value(2),["laborHours"]=Value(5),["equipmentHours"]=Value(4),["openingCount"]=Value(3)};
        doc.Options[0].Items[0].QuantityKey=key;
        var result=EstimatePricingEngine.Calculate(doc,Policy(),null,0);
        Assert.Empty(result.Blockers);Assert.Equal(expected,result.Options[0].Lines[0].Quantity);
        Assert.DoesNotContain(EstimatePricingEngine.Fields(trade,Policy()),x=>x.Key=="depth");
    }
    [Fact] public void UnconfirmedInputsMissingTaxAndSampleCatalogBlockPricing()
    {
        var doc=Document("concrete");var policy=Policy();policy.TaxPercent=null;policy.Catalog[0].Sample=true;
        var result=EstimatePricingEngine.Calculate(doc,policy,null,0);
        Assert.Contains(result.Blockers,x=>x.Contains("Length"));Assert.Contains(result.Blockers,x=>x.Contains("tax"));Assert.Contains(result.Blockers,x=>x.Contains("Unavailable"));Assert.Equal(0,result.BaseTotal);
    }
    [Fact] public void CustomerAgreementOverridesTenantRateAndAuthorizedDiscount()
    {
        var policy=Policy();var customer=Guid.NewGuid();policy.CustomerPrices[$"{customer}:item"]=80;policy.CustomerDiscounts[customer.ToString()]=15;
        var result=EstimatePricingEngine.Calculate(Document(),policy,customer,5);
        Assert.Equal(80,result.Options[0].Lines[0].UnitPrice);Assert.Equal(15,result.DiscountPercent);Assert.Empty(result.ApprovalReasons);
    }
    [Fact] public void OverridesMarginsAndTermsProduceExplicitApprovalReasons()
    {
        var policy=Policy();policy.AllowManualOverrides=true;policy.MinimumMarginPercent=50;policy.ApprovalAboveTotal=20;
        var doc=Document();doc.Options[0].Items[0].OverridePrice=30;doc.Options[0].Items[0].OverrideReason="Reviewed exception";doc.Terms="Net 90";
        var result=EstimatePricingEngine.Calculate(doc,policy,null,25);
        Assert.Empty(result.Blockers);Assert.Contains(result.ApprovalReasons,x=>x.Contains("discount"));Assert.Contains(result.ApprovalReasons,x=>x.Contains("margin"));Assert.Contains(result.ApprovalReasons,x=>x.Contains("terms"));Assert.Contains(result.ApprovalReasons,x=>x.Contains("override"));
    }
    [Fact] public void CostPlusUsesConfiguredCostAndMarkupAndRequiresCostBasis()
    {
        var policy=Policy();policy.Catalog[0].MarkupPercent=25;
        Assert.Equal(50,EstimatePricingEngine.Calculate(Document(),policy,null,0).Options[0].Lines[0].UnitPrice);
        policy.Catalog[0].UnitCost=null;Assert.Contains(EstimatePricingEngine.Calculate(Document(),policy,null,0).Blockers,x=>x.Contains("cost basis"));
    }
    [Fact] public void RequiredTradeOverridesDoNotRemoveCoreMeasurementGuards()
    {
        var policy=Policy();policy.RequiredFields["concrete"]=new(){["removalArea"]="Removal area"};
        var fields=EstimatePricingEngine.Fields("concrete",policy);Assert.Contains(fields,x=>x.Key=="depth");Assert.Contains(fields,x=>x.Key=="removalArea");Assert.DoesNotContain(EstimatePricingEngine.Fields("framing",policy),x=>x.Key=="removalArea");
    }
    [Fact] public async Task HandoffCarriesIdentityAndSourceButNeverConfirmsInferredDimensions()
    {
        var f=new Fixture();var p=await f.Create();Assert.Equal(f.Id,p.LeadId);Assert.Equal(f.Customer,p.CustomerId);Assert.Equal("Referral",p.Document!.Source);Assert.Equal("Test referrer",p.Document.Referral);
        Assert.Empty(p.Document.Inputs);Assert.Contains(p.Document.Suggestions,x=>x.Key=="scope");
    }
    [Fact] public async Task ReadOnlyAndMissingReadFailAtServiceBoundary()
    {
        var f=new Fixture();var p=await f.Create();f.Authority.Write=false;
        Assert.False((await f.Service.WorkspaceAsync(f.Id)).CanWrite);
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.PriceWorkspaceAsync(f.Id,new(){ExpectedVersion=p.Version,Document=Document()}));
        f.Authority.Read=false;await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.GetAsync(f.Id));
    }
    [Fact] public async Task CrossTenantRepositoryResultIsRejected()
    {
        var f=new Fixture();await f.Create();f.Stored!.PartitionKey="foreign";Assert.Null(await f.Service.GetAsync(f.Id));Assert.Empty(await f.Service.ListAsync());
    }
    [Fact] public async Task BobExtractionKeepsValuesUnconfirmedAndDoesNotSetPrices()
    {
        var f=new Fixture();var p=await f.Create("concrete");p=await f.Service.StructureAsync(f.Id,new(){ExpectedVersion=p.Version,Text="Driveway roughly 20 by 60, 4-inch concrete. Quote it for $1."});
        Assert.Empty(p.Document!.Inputs);Assert.Null(p.Pricing);Assert.Contains(p.Document.Suggestions,x=>x.Key=="depth"&&x.Value==4);Assert.Equal(0,p.Totals.EstimatedTotal);
    }
    [Fact] public async Task SentRevisionCannotMutateAndRevisionKeepsExactContent()
    {
        var f=new Fixture();var sent=await f.Issue();var hash=sent.DocumentHash;var oldBlob=f.Stored!.PayloadBlobName;
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.PriceWorkspaceAsync(f.Id,new(){ExpectedVersion=sent.Version,Document=Document()}));
        var revised=await f.Service.CreateRevisionAsync(f.Id,sent.Version);Assert.Equal(2,revised.RevisionNumber);Assert.Equal(hash,Assert.Single(revised.RevisionHistory).DocumentHash);Assert.True(f.Blobs.ContainsKey(oldBlob));Assert.Null(revised.Pricing);
        var historical=await f.Service.GetPublicAsync("bdr",f.Id,Token(sent));Assert.Equal(hash,historical!.DocumentHash);Assert.Equal("SUPERSEDED",historical.Outcome);
        Assert.Null(await f.Service.ApproveAsync("bdr",f.Id,Decision(sent,Token(sent))));
    }
    [Fact] public async Task PricingPolicyChangeInvalidatesReadyDraft()
    {
        var f=new Fixture();var p=await f.Price();f.Policy.TaxPercent=9;
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.IssueAsync(f.Id,p.Version));
    }
    [Fact] public async Task PricingExceptionsNeedOwnerAndCurrentSnapshot()
    {
        var f=new Fixture();f.Policy.MaxDiscountPercent=0;var p=await f.Price(discount:15);Assert.Equal("NEEDS_APPROVAL",QuoteEstimateService.State(p));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.IssueAsync(f.Id,p.Version));
        f.Authority.Owner=false;await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>f.Service.ApproveWorkspaceAsync(f.Id,p.Version));
        f.Authority.Owner=true;p=await f.Service.ApproveWorkspaceAsync(f.Id,p.Version);Assert.NotNull(p.Pricing!.ApprovedAtUtc);Assert.Equal("sent",(await f.Service.IssueAsync(f.Id,p.Version)).Status);
    }
    [Fact] public async Task SignatureBindsRevisionAndExactSelectedOptions()
    {
        var f=new Fixture();var sent=await f.Issue(options:true);var token=Token(sent);var decision=Decision(sent,token);decision.SelectedOptionIds=["base","upgrade"];
        decision.DocumentHash="changed";await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApproveAsync("bdr",f.Id,decision));decision.DocumentHash=sent.DocumentHash;
        var accepted=(await f.Service.ApproveAsync("bdr",f.Id,decision))!;Assert.Equal(216,accepted.ApprovalSignature!.Total);Assert.Equal(sent.DocumentHash,accepted.DocumentHash);Assert.Equal("ACCEPTED",QuoteEstimateService.State(accepted));
        var repeated=(await f.Service.ApproveAsync("bdr",f.Id,decision))!;Assert.Equal(accepted.ApprovalSignature.SignedAtUtc,repeated.ApprovalSignature!.SignedAtUtc);
        var next=await f.Service.CreateRevisionAsync(f.Id,repeated.Version);Assert.NotNull(next.RevisionHistory[0].ApprovalSignature);Assert.Null(next.ApprovalSignature);
    }
    [Fact] public async Task InvalidOptionSelectionCannotBeSigned()
    {
        var f=new Fixture();var sent=await f.Issue();var decision=Decision(sent,Token(sent));decision.SelectedOptionIds=["fake"];
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApproveAsync("bdr",f.Id,decision));
    }
    [Fact] public async Task PublicAndNonOwnerProjectionNeverContainsCostBasis()
    {
        var f=new Fixture();var p=await f.Price();p.Notes="Private note";p.Document!.LeadContext["secret"]="Private qualification";
        var customer=QuoteEstimateService.CustomerProjection(p);Assert.Equal("",customer.Notes);Assert.Empty(customer.Document!.LeadContext);Assert.Null(customer.Pricing!.Options[0].Cost);Assert.Null(customer.Pricing.Options[0].Lines[0].Cost);
        Assert.NotNull(p.Pricing!.Options[0].Cost);
        f.Authority.Owner=false;Assert.Null((await f.Service.WorkspaceAsync(f.Id)).Packet.Pricing!.Options[0].Cost);
    }
    [Fact] public async Task StalePriceSubmissionCannotOverwriteNewerDraft()
    {var f=new Fixture();var p=await f.Price();await f.Service.StructureAsync(f.Id,new(){ExpectedVersion=p.Version,Text="New scope notes"});await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.PriceWorkspaceAsync(f.Id,new(){ExpectedVersion=p.Version,Document=Document()}));}

    [Fact] public async Task AlternativeSelectionRequiresExactlyOneAndPreservesSignedArchive()
    {
        var f=new Fixture();var p=await f.Create();var doc=Document();
        doc.Options[0].Required=false;doc.Options[0].ExclusiveGroup="repair-or-replace";
        doc.Options.Add(new(){Id="replace",Name="Replace",Required=false,ExclusiveGroup="repair-or-replace",Items=[new(){CatalogId="item",Quantity=2,Confirmed=true}]});
        p=await f.Service.PriceWorkspaceAsync(f.Id,new(){ExpectedVersion=p.Version,Document=doc});p=await f.Service.IssueAsync(f.Id,p.Version);
        var decision=Decision(p,Token(p));decision.SelectedOptionIds=[];
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApproveAsync("bdr",f.Id,decision));
        decision.SelectedOptionIds=["base","replace"];await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApproveAsync("bdr",f.Id,decision));
        decision.SelectedOptionIds=["replace"];var accepted=(await f.Service.ApproveAsync("bdr",f.Id,decision))!;
        Assert.Equal(216,accepted.AcceptedTotal);await f.Service.CreateRevisionAsync(f.Id,accepted.Version);
        var history=(await f.Service.GetPublicAsync("bdr",f.Id,Token(p)))!;Assert.Equal(accepted.ApprovalSignature!.SignedAtUtc,history.ApprovalSignature!.SignedAtUtc);Assert.Equal(new[]{"replace"},history.AcceptedOptionIds);
    }
    [Fact] public async Task BobCannotWriteThroughAReadOnlyEstimateModule()
    {
        var f=new Fixture();var packet=await f.Create();f.Authority.Write=false;
        var provider=new BobEstimateActionProvider(f.Service,null!,"estimate.extract");
        await Assert.ThrowsAsync<MedInsights.Lib.ForbiddenAccessException>(()=>provider.ExecuteAsync(null!,JsonSerializer.SerializeToElement(new{estimateId=f.Id,expectedVersion=packet.Version,text="20 by 60"})));
    }

    [Fact] public async Task IssuedAttachmentBytesSurviveSourceRemovalAndNewRevision()
    {
        var f=new Fixture();var attachment=f.AddAttachment();var sent=await f.Issue();var frozen=Assert.Single(sent.Document!.Attachments);
        Assert.NotEmpty(frozen.ContentHash);Assert.NotEqual(attachment.BlobName,frozen.BlobName);f.Blobs.Remove(attachment.BlobName!);
        await f.Service.CreateRevisionAsync(f.Id,sent.Version);
        var download=await f.Service.DownloadProposalFileAsync("bdr",f.Id,attachment.Id,Token(sent));Assert.NotNull(download);
        using var reader=new StreamReader(download.Content);Assert.Equal("scope-photo-bytes",await reader.ReadToEndAsync());
        Assert.Null(await f.Service.DownloadProposalFileAsync("bdr",f.Id,attachment.Id,"wrong-token"));
    }

    [Fact] public async Task JobTimelineEventIsIdempotentAndCannotChangeSignedContent()
    {
        var f=new Fixture();var sent=await f.Issue();var accepted=(await f.Service.ApproveAsync("bdr",f.Id,Decision(sent,Token(sent))))!;
        await f.Service.RecordJobHandoffAsync(f.Id,f.Id);await f.Service.RecordJobHandoffAsync(f.Id,f.Id);
        var packet=(await f.Service.GetAsync(f.Id))!;Assert.Single(packet.Events.Where(x=>x.Type=="job-linked"));Assert.Equal(accepted.DocumentHash,packet.DocumentHash);Assert.Equal(accepted.ApprovalSignature!.SignedAtUtc,packet.ApprovalSignature!.SignedAtUtc);
    }

    private static EstimateInputValueDto Value(decimal n)=>new(){Value=n,Confirmed=true};
    private static EstimatePricingPolicyDto Policy()=>new(){TaxPercent=8,MaxDiscountPercent=10,Terms="Standard terms",Catalog=[new(){Id="item",Name="Configured service",Enabled=true,UnitPrice=100,UnitCost=40}]};
    private static EstimateDocumentDto Document(string trade="general")=>new(){TradeProfile=trade,Scope="Confirmed site scope",Terms="Standard terms",Options=[new(){Items=[new(){CatalogId="item",Quantity=1,Confirmed=true}]}]};
    private static string Token(QuoteEstimateDto p)=>p.Delivery!.ReviewUrl.Split("token=")[1];
    private static QuoteEstimateDecisionDto Decision(QuoteEstimateDto p,string token)=>new(){AccessToken=token,SignerPrintedName="Customer Test",IntentToSign=true,ConsentVersion=QuoteApprovalConsent.Version,RevisionNumber=p.RevisionNumber,DocumentHash=p.DocumentHash,SelectedOptionIds=["base"]};
    private sealed class Authority:IEstimateAuthority
    {public bool Read=true,Write=true,Owner=true;public Task RequireAsync(bool write,CancellationToken ct){if(!Read||write&&!Write)throw new MedInsights.Lib.ForbiddenAccessException("Denied");return Task.CompletedTask;}public Task<bool> CanWriteAsync(CancellationToken ct)=>Task.FromResult(Write);public Task<bool> CanApproveAsync(CancellationToken ct)=>Task.FromResult(Owner);}
    private sealed class User:IUserContext
    {public bool IsAuthenticated=>true;public Guid TenantId=>Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");public Guid UserId=>Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");public MedInsights.Lib.Utils.AppTimeZone Timezone=>MedInsights.Lib.Utils.AppTimeZone.Utc;public string FirstName=>"Estimator";public string LastName=>"Test";}
    private sealed class Resolver(Guid tenant):IQuoteRequestTenantResolver{public QuoteRequestTenantDefinition Resolve(string slug)=>new(){TenantId=tenant};}
    private sealed class Fixture
    {
        public Guid Id=Guid.NewGuid(),Customer=Guid.NewGuid();public QuoteEstimate? Stored;public EstimatePricingPolicyDto Policy=EstimateWorkspaceTests.Policy();
        public Authority Authority=new();public Dictionary<string,byte[]> Blobs=[];public QuoteEstimateService Service;private QuoteRequest _quote;private readonly User _user=new();
        public Fixture()
        {
            var pk=RepositoryKeyHelper.ToTenantPartitionKey(_user.TenantId);var repo=new Mock<IQuoteEstimateRepository>();
            var archives=new Dictionary<string,QuoteEstimate>();
            repo.Setup(x=>x.ArchiveAsync(It.IsAny<QuoteEstimate>(),It.IsAny<CancellationToken>())).Returns((QuoteEstimate e,CancellationToken ct)=>{archives[e.CustomerAccessTokenHash!]=JsonSerializer.Deserialize<QuoteEstimate>(JsonSerializer.Serialize(e))!;return Task.CompletedTask;});
            repo.Setup(x=>x.GetArchiveAsync(pk,Id,It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string p,Guid id,string hash,CancellationToken ct)=>archives.GetValueOrDefault(hash));
            repo.Setup(x=>x.GetAsync(pk,RepositoryKeyHelper.ToRowKey(Id),It.IsAny<CancellationToken>())).ReturnsAsync(()=>Stored);
            repo.Setup(x=>x.ListAsync(pk,It.IsAny<CancellationToken>())).ReturnsAsync(()=>Stored is null?[]:new[]{Stored});
            repo.Setup(x=>x.SaveAsync(It.IsAny<QuoteEstimate>(),It.IsAny<CancellationToken>())).ReturnsAsync((QuoteEstimate entity,CancellationToken ct)=>{entity.ETag=new ETag(Guid.NewGuid().ToString());Stored=entity;return entity;});
            _quote=TurnKeyOps.Services.Mappers.QuoteRequestMapper.ToEntity(new(){Id=Id,TenantId=_user.TenantId,ContactName="Customer Test",Email="customer@example.invalid",SiteName="Test site",Status="qualified",UpdatedAtUtc=DateTime.UtcNow});
            var requests=new Mock<IQuoteRequestRepository>();requests.Setup(x=>x.GetAsync(pk,RepositoryKeyHelper.ToRowKey(Id),It.IsAny<CancellationToken>())).ReturnsAsync(()=>_quote);
            requests.Setup(x=>x.SaveAsync(It.IsAny<QuoteRequest>(),It.IsAny<CancellationToken>())).ReturnsAsync((QuoteRequest q,CancellationToken ct)=>{_quote=q;return q;});
            var blobs=new Mock<IAzureBlobStorageService>();
            blobs.Setup(x=>x.UploadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<Stream>(),It.IsAny<string>(),It.IsAny<IReadOnlyDictionary<string,string>>(),It.IsAny<CancellationToken>())).Returns((string c,string name,Stream stream,string type,IReadOnlyDictionary<string,string> m,CancellationToken ct)=>{using var memory=new MemoryStream();stream.CopyTo(memory);Blobs[name]=memory.ToArray();return Task.CompletedTask;});
            blobs.Setup(x=>x.OpenReadAsync(It.IsAny<string>(),It.IsAny<string>(),It.IsAny<CancellationToken>())).ReturnsAsync((string c,string n,CancellationToken ct)=>(Stream)new MemoryStream(Blobs[n]));
            var settings=new Mock<ITenantSettingsRepository>();settings.Setup(x=>x.GetAsync(pk,"SETTINGS|OPERATIONAL",It.IsAny<CancellationToken>(),false)).ReturnsAsync(()=>new(){ValuesJson=JsonSerializer.Serialize(new{estimates=Policy},new JsonSerializerOptions(JsonSerializerDefaults.Web))});
            Service=new(repo.Object,requests.Object,blobs.Object,Mock.Of<IEstimateDefaultsService>(),new Resolver(_user.TenantId),_user,settings.Object,tenantOptions:Options.Create(new QuoteRequestTenantOptions{Tenants=new(){["bdr"]=new(){TenantId=_user.TenantId}}}),authority:Authority);
        }
        public QuoteRequestAttachmentDto AddAttachment()
        {
            var a=new QuoteRequestAttachmentDto{Id=Guid.NewGuid(),FileName="scope.jpg",ContentType="image/jpeg",BlobContainer="quote-request-attachments",BlobName=$"{_user.TenantId:N}/{Id:N}/source.jpg"};
            _quote.AttachmentsJson=JsonSerializer.Serialize(new[]{a},new JsonSerializerOptions(JsonSerializerDefaults.Web));Blobs[a.BlobName]=System.Text.Encoding.UTF8.GetBytes("scope-photo-bytes");return a;
        }
        public Task<QuoteEstimateDto> Create(string trade="general")=>Service.PrepareFromLeadAsync(new(){Id=Id,IntakeRequestId=Id,TenantId=_user.TenantId,CustomerId=Customer,ContactName="Customer Test",SiteAddress="Test site",RequestedWork="Confirmed site scope",TradeProfile=trade,Source="Referral",ReferralName="Test referrer"},Id);
        public async Task<QuoteEstimateDto> Price(bool options=false,decimal discount=0)
        {var p=await Create();var doc=Document();if(options)doc.Options.Add(new(){Id="upgrade",Name="Upgrade",Required=false,Items=[new(){CatalogId="item",Quantity=1,Confirmed=true}]});return await Service.PriceWorkspaceAsync(Id,new(){ExpectedVersion=p.Version,Document=doc,DiscountPercent=discount});}
        public async Task<QuoteEstimateDto> Issue(bool options=false){var p=await Price(options);return await Service.IssueAsync(Id,p.Version);}
    }
}
