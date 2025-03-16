using SPT.Reflection.Utils;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SPT.Common.Http;
using Newtonsoft.Json;
using System;
using UnityEngine;
using System.Diagnostics;
using System.Threading;

internal static class TraderClassExtensions
{
    private static ISession _Session;
    private static ISession Session => _Session ??= ClientAppUtils.GetMainApp().GetClientBackEndSession();

    private static readonly FieldInfo SupplyDataField =
        typeof(TraderClass).GetField("supplyData_0", BindingFlags.NonPublic | BindingFlags.Instance);

    public static SupplyData GetSupplyData(this TraderClass trader) =>
        SupplyDataField.GetValue(trader) as SupplyData;

    public static void SetSupplyData(this TraderClass trader, SupplyData supplyData) =>
        SupplyDataField.SetValue(trader, supplyData);

    public static async void UpdateSupplyData(this TraderClass trader)
    {
        Result<SupplyData> result = await Session.GetSupplyData(trader.Id);
        if (result.Succeed)
            trader.SetSupplyData(result.Value);
        else
            UnityEngine.Debug.LogError("Failed to download supply data");
    }
}

class ItemExtensions
{
    public static ISession Session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
    static Dictionary<string, int> fleaCache = new Dictionary<string, int>();
    static Dictionary<string, int> traderCache = new Dictionary<string, int>();

    public sealed class TraderOffer
    {
        public string Name;
        public int Price;
        public double Course;
        public int Count;

        public TraderOffer(string name, int price, double course, int count)
        {
            Name = name;
            Price = price;
            Course = course;
            Count = count;
        }
    }

    public static TraderOffer? GetTraderOffer(Item item, TraderClass trader)
    {
        try {
            var result = trader.GetUserItemPrice(item);
            return result is null ? null : new(
                trader.LocalizedName,
                result.Value.Amount,
                trader.GetSupplyData().CurrencyCourses[result.Value.CurrencyId],
                item.StackObjectsCount
            );
        }
        catch
        {
            return null;
        }
    }

    public static IEnumerable<TraderOffer?>? GetAllTraderOffers(Item item)
    {
        if (!Session.Profile.Examined(item))
            return null;
        switch (item.Owner?.OwnerType)
        {
            case EOwnerType.RagFair:
            case EOwnerType.Trader:
                if (item.StackObjectsCount > 1 || item.UnlimitedCount)
                {
                    item = item.CloneItem();
                    item.StackObjectsCount = 1;
                    item.UnlimitedCount = false;
                }
                break;
        }
        return Session.Traders
            .Select(trader => GetTraderOffer(item, trader))
            .Where(offer => offer != null)
            .OrderByDescending(offer => offer?.Price * offer?.Course);
    }

    public static void CacheFleaPrice(Item item)
    {
        var ragFairClass = Session.RagFair;
        if (!ragFairClass.Available || fleaCache.ContainsKey(item.Name))
        {
            return;
        }
        ragFairClass.GetMarketPrices(item.TemplateId, result => {
            fleaCache[item.Name] = (int) result.avg;
        });
    }

    public static int GetFleaPrice(Item item)
    {
        var ragFairClass = Session.RagFair;
        if (!ragFairClass.Available)
        {
            return 0;
        }

        if (fleaCache.ContainsKey(item.Name))
        {
            //UnityEngine.Debug.LogError($"Get flea price: {item.Name} {fleaCache[item.Name]}");
            return fleaCache[item.Name];
        }
        else
        {
            return -1;
        }
    }

    public static int GetBestTraderPrice(Item item)
    {
        if (traderCache.ContainsKey(item.Name))
        {
            return traderCache[item.Name];
        }

        var offer = GetAllTraderOffers(item)?.FirstOrDefault() ?? null;
        var price = 0;
        if (offer != null)
        {
            price = offer.Price;
        }
        traderCache[item.Name] = price;
        return price;
    }
        

    public static int GetBestPrice(Item item)
    {                                                                                 
        var fleaPrice = GetFleaPrice(item);
        var traderPrice = GetBestTraderPrice(item);
        return fleaPrice > traderPrice ? fleaPrice : traderPrice;
    }
}