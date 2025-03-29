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
using System.Threading.Tasks;
using System.Collections;

internal static class TraderClassExtensions
{
    private static ISession Session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
    public static bool IsInit = false;
    public static async Task UpdateSupplyData(this TraderClass trader)
    {
        try
        {
            Result<SupplyData> result = await Session.GetSupplyData(trader.Id);
            if (result.Succeed)
            {
                trader.supplyData_0 = result.Value;
            }
            else
            {
                UnityEngine.Debug.LogError($"Failed to download supply data for trader {trader.Id}");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"Error updating supply data for trader {trader.Id}: {ex}");
        }
    }
}

class ItemExtensions : MonoBehaviour
{
    public static ISession Session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
    static Dictionary<string, float>? fleaCache = null;
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

    public static void Init(MonoBehaviour coroutineRunner)
    {
        if (TraderClassExtensions.IsInit)
        {
            return;
        }

        coroutineRunner.StartCoroutine(InitCoroutine());
    }

    private static IEnumerator InitCoroutine()
    {
        // Start trader updates
        var traderTasks = new List<Task>();
        foreach (var trader in Session.Traders)
        {
            traderTasks.Add(TraderClassExtensions.UpdateSupplyData(trader));
        }

        // Start flea prices request
        var fleaPricesCompleted = false;
        Session.RagfairGetPrices(result =>
        {
            if (result.Succeed && result.Value != null)
            {
                fleaCache = result.Value;
            }
            else
            {
                UnityEngine.Debug.LogError("[RADAR] Failed to get Ragfair database");
            }
            fleaPricesCompleted = true;
        });

        // Wait for all traders to complete
        while (traderTasks.Any(t => !t.IsCompleted))
        {
            yield return null;
        }

        // Wait for flea prices to complete
        while (!fleaPricesCompleted)
        {
            yield return null;
        }

        TraderClassExtensions.IsInit = true;
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

    public static int GetFleaPrice(Item item)
    {
        if (fleaCache != null && fleaCache.ContainsKey(item.TemplateId))
        {
            return (int) fleaCache[item.TemplateId];
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
            traderCache[item.Name] = price;
        }
        
        return price;
    }

    public static int GetBestPrice(Item item)
    {
        var fleaPrice = GetFleaPrice(item);
        var traderPrice = GetBestTraderPrice(item);
        return fleaPrice > traderPrice ? fleaPrice : traderPrice;
    }
}