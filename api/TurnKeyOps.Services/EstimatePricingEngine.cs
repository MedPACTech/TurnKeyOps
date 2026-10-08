using System.Security.Cryptography;
using System.Text.Json;
using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services;

public static class EstimatePricingEngine
{
    public static readonly string[] Trades = ["general", "concrete", "framing", "land-clearing", "doors-locks"];
    public static IReadOnlyList<EstimateFieldDto> Fields(string trade, EstimatePricingPolicyDto policy)
    {
        List<EstimateFieldDto> fields = trade switch {
            "concrete" => [new("length","Length","ft"),new("width","Width","ft"),new("depth","Depth","in"),new("waste","Waste allowance","%")],
            "framing" => [new("length","Wall length","ft"),new("height","Wall height","ft"),new("spacing","Stud spacing","in"),new("openings","Additional opening studs","each")],
            "land-clearing" => [new("acreage","Area","acres"),new("laborHours","Confirmed labor","hours"),new("equipmentHours","Confirmed equipment","hours")],
            "doors-locks" => [new("openingCount","Openings","each"),new("laborHours","Confirmed labor","hours")],
            _ => []
        };
        if (policy.RequiredFields.TryGetValue(trade, out var extra))
            fields.AddRange(extra.Where(x=>fields.All(f=>f.Key!=x.Key)).Select(x=>new EstimateFieldDto(x.Key,x.Value,"")));
        return fields;
    }
    public static string Version(EstimatePricingPolicyDto policy) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(policy))).ToLowerInvariant();
    public static void Validate(EstimatePricingPolicyDto p)
    {
        if (p.Catalog.Count>500 || p.Catalog.Select(x=>x.Id).Distinct().Count()!=p.Catalog.Count || p.Catalog.Any(x=>string.IsNullOrWhiteSpace(x.Id)||x.Id.Length>80||string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>200||x.UnitPrice<0||x.UnitPrice>10000000||x.UnitCost<0||x.UnitCost>10000000||x.MarkupPercent<0||x.MarkupPercent>1000||!Trades.Contains(x.TradeProfile)||!new[]{"material","labor","equipment","service","subcontract","fee","allowance"}.Contains(x.Kind)))
            throw new ArgumentException("Catalog items need unique IDs, supported trades/kinds, names and non-negative prices/costs; maximum 500 items.");
        if(p.TaxPercent is <0 or >100 || p.MaxDiscountPercent is <0 or >100 || p.MinimumMarginPercent is <0 or >100 || p.DepositPercent is <0 or >100 || p.MinimumCharge<0 || p.ApprovalAboveTotal<0 || p.CustomerDiscounts.Any(x=>!Guid.TryParse(x.Key,out _)||x.Value<0||x.Value>100) || p.CustomerPrices.Any(x=>x.Value<0||x.Value>10000000))
            throw new ArgumentException("Pricing percentages and customer agreements must be valid and non-negative.");
        if(p.StageLabels.Count>30||p.StageLabels.Any(x=>x.Key.Length>80||x.Value.Length>100))throw new ArgumentException("Use short canonical stage labels.");
        if(p.CompanyName.Length>200||p.PublicOrigin.Length>300)throw new ArgumentException("Company name or origin is too long.");
        if(p.RequiredFields.Any(x=>!Trades.Contains(x.Key)||x.Value.Count>30||x.Value.Any(f=>f.Key.Length>80||f.Value.Length>200))||p.Terms.Length>8000)
            throw new ArgumentException("Invalid trade schema or terms.");
    }
    public static EstimatePricingDto Calculate(EstimateDocumentDto doc, EstimatePricingPolicyDto policy, Guid? customerId, decimal discount)
    {
        Validate(policy);
        if(!Trades.Contains(doc.TradeProfile)||doc.Inputs.Count>60||doc.Options.Count is <1 or >10||doc.Options.Select(x=>x.Id).Distinct().Count()!=doc.Options.Count||doc.Options.Any(x=>string.IsNullOrWhiteSpace(x.Id)||x.Id.Length>80||x.ExclusiveGroup.Length>80||x.Items.Count>100||x.Items.Any(i=>i.OverrideReason.Length>1000||i.QuantityKey.Length>80))||doc.Timing.Length>2000||doc.Inputs.Any(x=>x.Key.Length>80)||doc.Scope.Length>10000||doc.Terms.Length>8000||doc.Exclusions.Length>8000||doc.ValidDays is <1 or >365||doc.DepositPercent is <0 or >100||discount is <0 or >100)
            throw new ArgumentException("Invalid scope, options, validity or percentages.");
        var result=new EstimatePricingDto{RuleVersion=Version(policy),CalculatedAtUtc=DateTime.UtcNow,TaxPercent=policy.TaxPercent??0};
        var quantities=new Dictionary<string,decimal>();
        foreach(var field in Fields(doc.TradeProfile,policy))
        {
            if(!doc.Inputs.TryGetValue(field.Key,out var input)||!input.Confirmed) result.Blockers.Add($"Confirm {field.Label} ({field.Unit}).");
            else if(input.Value<0||input.Value>1000000||input.Value==0 && field.Key is "length" or "width" or "depth" or "height" or "spacing" or "acreage" or "openingCount") result.Blockers.Add($"Enter a valid {field.Label}.");
            else quantities[field.Key]=input.Value;
        }
        if(result.Blockers.Count==0)
        {
            decimal V(string k)=>quantities.GetValueOrDefault(k);
            if(doc.TradeProfile=="concrete") { quantities["squareFeet"]=V("length")*V("width"); quantities["cubicYards"]=quantities["squareFeet"]*V("depth")/324m*(1+V("waste")/100m); quantities["perimeter"]=2*(V("length")+V("width")); }
            if(doc.TradeProfile=="framing") { quantities["squareFeet"]=V("length")*V("height"); quantities["studCount"]=Math.Ceiling(V("length")*12/V("spacing"))+1+V("openings"); }
        }
        if(policy.TaxPercent is null) result.Blockers.Add("Configure tenant tax, including an explicit 0% when applicable.");
        if(string.IsNullOrWhiteSpace(doc.Scope)) result.Blockers.Add("Confirm the scope of work.");
        if(doc.Options.Any(x=>x.Required&&x.ExclusiveGroup!=""))throw new ArgumentException("Choose-one alternatives cannot also be required individual items.");
        if(!doc.Options.Any(x=>x.Required||x.ExclusiveGroup!="")) result.Blockers.Add("Include a required base scope.");
        var agreement=customerId.HasValue?policy.CustomerDiscounts.GetValueOrDefault(customerId.Value.ToString()):0;
        result.DiscountPercent=Math.Max(discount,agreement);
        if(discount>Math.Max(policy.MaxDiscountPercent,agreement)) result.ApprovalReasons.Add("Requested discount exceeds tenant/customer authority.");
        if(doc.Terms!=policy.Terms) result.ApprovalReasons.Add("Payment/contract terms differ from tenant defaults.");
        if(doc.DepositPercent!=policy.DepositPercent) result.ApprovalReasons.Add("Deposit terms differ from tenant defaults.");
        foreach(var option in doc.Options)
        {
            var priced=new EstimateOptionPriceDto{Id=option.Id,Name=option.Name,Required=option.Required,ExclusiveGroup=option.ExclusiveGroup};
            if(string.IsNullOrWhiteSpace(option.Name)||option.Name.Length>200)throw new ArgumentException("Provide a short option name.");
            if(option.Items.Count==0)result.Blockers.Add($"Choose catalog items for {option.Name}.");
            foreach(var selected in option.Items)
            {
                var catalog=policy.Catalog.SingleOrDefault(x=>x.Id==selected.CatalogId);
                if(catalog is null||!catalog.Enabled||catalog.Sample||catalog.TradeProfile!="general"&&catalog.TradeProfile!=doc.TradeProfile){result.Blockers.Add($"Unavailable or wrong-trade catalog item: {selected.CatalogId}.");continue;}
                var quantity=selected.Quantity;
                if(selected.QuantityKey!="manual")
                {if(!quantities.TryGetValue(selected.QuantityKey,out quantity)){result.Blockers.Add($"Confirm inputs for {catalog.Name} ({selected.QuantityKey}).");continue;}}
                else if(!selected.Confirmed){result.Blockers.Add($"Confirm quantity for {catalog.Name}.");continue;}
                if(quantity<=0||quantity>100000000){result.Blockers.Add($"Quantity for {catalog.Name} is outside supported limits.");continue;}
                var price=catalog.UnitPrice;
                var rule="Tenant catalog";
                if(catalog.MarkupPercent.HasValue)
                {
                    if(!catalog.UnitCost.HasValue){result.Blockers.Add($"A configured cost basis is required for cost-plus item {catalog.Name}.");continue;}
                    price=Money(catalog.UnitCost.Value*(1+catalog.MarkupPercent.Value/100));rule="Tenant cost-plus rule";
                }
                if(customerId.HasValue&&policy.CustomerPrices.TryGetValue($"{customerId.Value}:{catalog.Id}",out var customerPrice)){price=customerPrice;rule="Customer agreement";}
                if(selected.OverridePrice.HasValue)
                {
                    if(!policy.AllowManualOverrides||selected.OverridePrice<0||selected.OverridePrice>10000000||string.IsNullOrWhiteSpace(selected.OverrideReason)) {result.Blockers.Add("Manual price override requires configured permission and a reason.");continue;}
                    price=selected.OverridePrice.Value;rule="Reviewed project override: "+selected.OverrideReason;result.ApprovalReasons.Add("Manual price override requires office approval.");
                }
                priced.Lines.Add(new(){CatalogId=catalog.Id,Name=catalog.Name,Kind=catalog.Kind,Unit=catalog.Unit,Quantity=decimal.Round(quantity,4),UnitPrice=price,Total=Money(decimal.Round(quantity,4)*price),Cost=catalog.UnitCost.HasValue?Money(decimal.Round(quantity,4)*catalog.UnitCost.Value):null,Taxable=catalog.Taxable,Rule=rule});
            }
            priced.Subtotal=priced.Lines.Sum(x=>x.Total);
            priced.Discount=Money(priced.Subtotal*result.DiscountPercent/100);
            priced.Tax=Money(priced.Lines.Where(x=>x.Taxable).Sum(x=>x.Total)*(1-result.DiscountPercent/100)*result.TaxPercent/100);
            priced.Total=priced.Subtotal-priced.Discount+priced.Tax;
            if(priced.Lines.All(x=>x.Cost.HasValue)){priced.Cost=priced.Lines.Sum(x=>x.Cost??0);var net=priced.Subtotal-priced.Discount;priced.MarginPercent=net>0?decimal.Round((net-priced.Cost.Value)/net*100,2):null;}
            else result.ApprovalReasons.Add("Missing cost basis requires office review.");
            if(policy.MinimumMarginPercent.HasValue&&(priced.MarginPercent is null||priced.MarginPercent<policy.MinimumMarginPercent))result.ApprovalReasons.Add($"{option.Name} is below the configured margin target.");
            result.Options.Add(priced);
        }
        if(result.BaseTotal<policy.MinimumCharge)result.ApprovalReasons.Add("Base price is below the minimum charge.");
        if(policy.ApprovalAboveTotal.HasValue&&result.Options.Sum(x=>x.Total)>policy.ApprovalAboveTotal)result.ApprovalReasons.Add("Total including options exceeds the approval threshold.");
        result.ApprovalReasons=result.ApprovalReasons.Distinct().ToList();result.Blockers=result.Blockers.Distinct().ToList();return result;
    }
    private static decimal Money(decimal value)=>Math.Round(value,2,MidpointRounding.AwayFromZero);
}
