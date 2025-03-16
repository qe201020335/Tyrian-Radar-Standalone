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
using static System.Collections.Specialized.BitVector32;

internal static class TraderClassExtensions
{
    private static ISession Session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
    public static async void UpdateSupplyData(this TraderClass trader)
    {
        Result<SupplyData> result = await Session.GetSupplyData(trader.Id);
        if (result.Succeed)
        {
            trader.supplyData_0 = result.Value;
        }
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

    public static void Init()
    {
        foreach (var trader in Session.Traders)
        {
            TraderClassExtensions.UpdateSupplyData(trader);
        }
    }

    public static TraderOffer? GetTraderOffer(Item item, TraderClass trader)
    {
        if (trader.supplyData_0 != null)
        {
            var result = trader.GetUserItemPrice(item);
            return result is null ? null : new(
                trader.LocalizedName,
                result.Value.Amount,
                trader.Dictionary_0[result.Value.CurrencyId],
                item.StackObjectsCount
            );
        }
        else
        {
            return null;
        }
    }

    public static IEnumerable<TraderOffer?>? GetAllTraderOffers(Item item)
    {
        //if (!Session.Profile.Examined(item))
        //    return null;
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
        if (fleaCache.ContainsKey(item.Name) || !ragFairClass.Available)
        {
            return;
        }

        try
        {
            ragFairClass.GetMarketPrices(item.TemplateId, result =>
            {
                if (result == null)
                {
                    UnityEngine.Debug.LogError($"Error: Received null result for item {item.Name}");
                    return;
                }

                fleaCache[item.Name] = (int)result.avg;
            });
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Unexpected error in CacheFleaPrice for {item.Name}: {ex.Message}");
        }
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