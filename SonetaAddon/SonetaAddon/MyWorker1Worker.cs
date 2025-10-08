// Decompiled with JetBrains decompiler
// Type: Soneta.EI.OptimaDocument
// Assembly: Soneta.EI, Version=2510.0.0.0, Culture=neutral, PublicKeyToken=a19fc6236fd34393
// MVID: 4669A27A-C686-44C9-8329-B2330FC3269F
// Assembly location: D:\Soneta\enova365 2510.0.0-beta.185782\Soneta.Standard\Soneta.Products.Server.Standard\Soneta.EI.dll

using Microsoft.Extensions.DependencyInjection;
using Soneta.Business;
using Soneta.Business.Licence;
using Soneta.Core;
using Soneta.CRM;
using Soneta.EwidencjaVat;
using Soneta.Kadry;
using Soneta.Kasa;
using Soneta.Kasa.Extensions;
using Soneta.Ksiega;
using Soneta.Langs;
using Soneta.Tools;
using Soneta.Types;
using Soneta.Waluty;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

#nullable disable
namespace Soneta.EI;

internal sealed class OptimaDocument
{
  private const float VERSION_NONE = 0.0f;
  private const float VERSION_1 = 1f;
  private const float VERSION_2 = 2f;
  private readonly Log importLog = new Log("Import".TranslateIgnore());
  private readonly Log progressLog = new Log("Progress");
  private readonly Session EnovaSession;
  private readonly OptimaFileImportFilter ImportFilter;
  private OptimaDocument.AdvancedReader AdvReader;
  private OptimaDocument.Parser AdvParser;
  private bool SettingSaveOA;
  private OptimaDocument.Banki CtnBanki;
  private OptimaDocument.Urzedy CtnUrzedy;
  private OptimaDocument.Pracownicy CtnPracownicy;
  private OptimaDocument.Wspolnicy CtnWspolnicy;
  private OptimaDocument.Kontrahenci CtnKontrahenci;
  private OptimaDocument.RaportyKB CtnRaportyKB;
  private OptimaDocument.RejestrySprzedazyVAT CtnRejestrSprzedazy;
  private OptimaDocument.RejestryZakupuVAT CtnRejestrZakupu;

  private static string ERROR_LOG => "{0}\r\n\tBłąd: {1}".Translate();

  internal OptimaDocument(OptimaFileImportFilter filter)
  {
    this.ImportFilter = filter;
    this.EnovaSession = filter.Session;
    this.SettingSaveOA = ServiceProviderServiceExtensions.GetService<IOptimaDokumentImportOaService>(filter.Session) != null;
  }

  internal void ReadData(XmlTextReader reader)
  {
    TraceInfo.ShowOutput(this.importLog.Category);
    this.AdvReader = new OptimaDocument.AdvancedReader(reader);
    this.AdvParser = new OptimaDocument.Parser(this);
    while (this.AdvReader.FollowToElement())
    {
      string name = this.AdvReader.Name;
      if (name != null)
      {
        switch (name.Length)
        {
          case 4:
            if (name == "ROOT")
            {
              this.AdvReader.Skip();
              continue;
            }
            break;
          case 5:
            if (name == "BANKI")
            {
              this.CtnBanki = new OptimaDocument.Banki(this);
              this.CtnBanki.InitializeCollection();
              continue;
            }
            break;
          case 6:
            if (name == "URZEDY")
            {
              this.CtnUrzedy = new OptimaDocument.Urzedy(this);
              this.CtnUrzedy.InitializeCollection();
              continue;
            }
            break;
          case 9:
            if (name == "WSPOLNICY")
            {
              this.CtnWspolnicy = new OptimaDocument.Wspolnicy(this);
              this.CtnWspolnicy.InitializeCollection();
              continue;
            }
            break;
          case 10:
            switch (name[0])
            {
              case 'P':
                if (name == "PRACOWNICY")
                {
                  this.CtnPracownicy = new OptimaDocument.Pracownicy(this);
                  this.CtnPracownicy.InitializeCollection();
                  continue;
                }
                break;
              case 'R':
                if (name == "RAPORTY_KB")
                {
                  this.CtnRaportyKB = new OptimaDocument.RaportyKB(this);
                  this.CtnRaportyKB.InitializeCollection();
                  continue;
                }
                break;
            }
            break;
          case 11:
            if (name == "KONTRAHENCI")
            {
              if (this.CtnKontrahenci == null)
              {
                this.CtnKontrahenci = new OptimaDocument.Kontrahenci(this);
                this.CtnKontrahenci.InitializeCollection();
                continue;
              }
              this.CtnKontrahenci.InitializeCollection();
              continue;
            }
            break;
          case 19:
            if (name == "REJESTRY_ZAKUPU_VAT")
            {
              if (this.CtnRejestrZakupu == null)
              {
                this.CtnRejestrZakupu = new OptimaDocument.RejestryZakupuVAT(this);
                this.CtnRejestrZakupu.InitializeCollection();
                continue;
              }
              this.CtnRejestrZakupu.InitializeCollection();
              continue;
            }
            break;
          case 22:
            if (name == "REJESTRY_SPRZEDAZY_VAT")
            {
              if (this.CtnRejestrSprzedazy == null)
              {
                this.CtnRejestrSprzedazy = new OptimaDocument.RejestrySprzedazyVAT(this);
                this.CtnRejestrSprzedazy.InitializeCollection();
                continue;
              }
              this.CtnRejestrSprzedazy.InitializeCollection();
              continue;
            }
            break;
        }
      }
      this.AdvReader.OmitElement();
    }
  }

  internal static ApplicationException InvalidFormatException(string format, params object[] prms)
  {
    return new ApplicationException("Niepoprawny format pliku wejściowego XML.\n".Translate() + string.Format(format, prms));
  }

  private int GetOstatniNumer(string symbol)
  {
    DokEwidencji prev = CoreModule.GetInstance((ISessionable) this.EnovaSession).DokEwidencja.NumerWgSymboluDokumentu[symbol].GetPrev(Array.Empty<object>());
    return prev == null ? 0 : prev.Numer.Numer;
  }

  public void Save(OptimaImportStats stats)
  {
    if (this.ImportFilter.Params.Kontrahenci && this.CtnKontrahenci != null)
      this.SaveKontrahenci(stats);
    if (this.ImportFilter.Params.Kontrahenci && this.CtnBanki != null)
      this.SaveBanki(stats);
    if (this.ImportFilter.Params.Kontrahenci && this.CtnUrzedy != null)
      this.SaveUrzedy(stats);
    if (this.ImportFilter.Params.Kontrahenci && this.CtnPracownicy != null)
      this.SavePracownicy(stats);
    if (this.ImportFilter.Params.Kontrahenci && this.CtnWspolnicy != null)
      this.SaveWspolnicy(stats);
    if (this.ImportFilter.Params.DokumentyHandlowe && this.ImportFilter.Params.Typ == TypDokumentu.SprzedażEwidencja && this.CtnRejestrSprzedazy != null)
      this.SaveRejestrSprzedazy(stats);
    if (this.ImportFilter.Params.DokumentyHandlowe && this.ImportFilter.Params.Typ == TypDokumentu.ZakupEwidencja && this.CtnRejestrZakupu != null)
      this.SaveRejestrZakupu(stats);
    if (!this.ImportFilter.Params.RaportyKB || this.CtnRaportyKB == null)
      return;
    this.SaveRaportyKB(stats);
  }

  private void SaveRejestrZakupu(OptimaImportStats stats)
  {
    foreach (OptimaDocument.RejestrZakupuVAT b in (ReadOnlyCollectionBase) this.CtnRejestrZakupu)
    {
      if (this.ImportFilter.Params.Zakres.Contains(b.DATA_WYSTAWIENIA) && (this.ImportFilter.Params.DefinicjaObca == "" || b.REJESTR == this.ImportFilter.Params.DefinicjaObca))
      {
        try
        {
          this.progressLog.WriteLine((object) b);
          using (ITransaction transaction = this.EnovaSession.Logout(true))
          {
            b.Save(stats);
            transaction.Commit();
          }
        }
        catch (Exception ex)
        {
          this.HandleException(stats, (object) b, ex);
        }
      }
    }
  }

  private void SaveRejestrSprzedazy(OptimaImportStats stats)
  {
    foreach (OptimaDocument.RejestrSprzedazyVAT b in (ReadOnlyCollectionBase) this.CtnRejestrSprzedazy)
    {
      if (this.ImportFilter.Params.Zakres.Contains(b.DATA_WYSTAWIENIA) && (this.ImportFilter.Params.DefinicjaObca == "" || b.REJESTR == this.ImportFilter.Params.DefinicjaObca) && (this.ImportFilter.Params.Paragony || !b.FISKALNA))
      {
        try
        {
          this.progressLog.WriteLine((object) b);
          using (ITransaction transaction = this.EnovaSession.Logout(true))
          {
            b.Save(stats);
            transaction.Commit();
          }
        }
        catch (Exception ex)
        {
          this.HandleException(stats, (object) b, ex);
        }
      }
    }
  }

  private void SaveRaportyKB(OptimaImportStats stats)
  {
    foreach (OptimaDocument.RaportKB b in (ReadOnlyCollectionBase) this.CtnRaportyKB)
    {
      if (this.ImportFilter.Params.Zakres.Contains(b.DATA_ZAMKNIECIA) && (this.ImportFilter.Params.EwidencjaObca == "" || this.ImportFilter.Params.EwidencjaObca == b.RACHUNEK.Kod))
      {
        try
        {
          this.progressLog.WriteLine((object) b);
          using (ITransaction transaction = this.EnovaSession.Logout(true))
          {
            b.Save(stats);
            transaction.Commit();
          }
        }
        catch (Exception ex)
        {
          this.HandleException(stats, (object) b, ex);
        }
      }
    }
  }

  private void SaveKontrahenci(OptimaImportStats stats)
  {
    foreach (OptimaDocument.Kontrahent b in (ReadOnlyCollectionBase) this.CtnKontrahenci)
    {
      try
      {
        this.progressLog.WriteLine((object) b);
        using (ITransaction transaction = this.EnovaSession.Logout(true))
        {
          b.Save();
          transaction.Commit();
        }
      }
      catch (Exception ex)
      {
        this.HandleException(stats, (object) b, ex);
      }
    }
  }

  private void SaveWspolnicy(OptimaImportStats stats)
  {
    foreach (OptimaDocument.Wspolnik b in (ReadOnlyCollectionBase) this.CtnWspolnicy)
    {
      try
      {
        this.progressLog.WriteLine((object) b);
        using (ITransaction transaction = this.EnovaSession.Logout(true))
        {
          b.Save();
          transaction.Commit();
        }
      }
      catch (Exception ex)
      {
        this.HandleException(stats, (object) b, ex);
      }
    }
  }

  private void SavePracownicy(OptimaImportStats stats)
  {
    foreach (OptimaDocument.Pracownik b in (ReadOnlyCollectionBase) this.CtnPracownicy)
    {
      try
      {
        this.progressLog.WriteLine((object) b);
        using (ITransaction transaction = this.EnovaSession.Logout(true))
        {
          b.Save();
          transaction.Commit();
        }
      }
      catch (Exception ex)
      {
        this.HandleException(stats, (object) b, ex);
      }
    }
  }

  private void SaveUrzedy(OptimaImportStats stats)
  {
    foreach (OptimaDocument.Urzad b in (ReadOnlyCollectionBase) this.CtnUrzedy)
    {
      try
      {
        this.progressLog.WriteLine((object) b);
        using (ITransaction transaction = this.EnovaSession.Logout(true))
        {
          b.Save();
          transaction.Commit();
        }
      }
      catch (Exception ex)
      {
        this.HandleException(stats, (object) b, ex);
      }
    }
  }

  private void SaveBanki(OptimaImportStats stats)
  {
    foreach (OptimaDocument.Bank b in (ReadOnlyCollectionBase) this.CtnBanki)
    {
      try
      {
        this.progressLog.WriteLine((object) b);
        using (ITransaction transaction = this.EnovaSession.Logout(true))
        {
          b.Save();
          transaction.Commit();
        }
      }
      catch (Exception ex)
      {
        this.HandleException(stats, (object) b, ex);
      }
    }
  }

  private void HandleException(OptimaImportStats stats, object b, Exception e)
  {
    this.importLog.WriteLine(string.Format(OptimaDocument.ERROR_LOG, b, (object) e.Message));
    ++stats.Errors;
  }

  private sealed class AdvancedReader
  {
    private XmlTextReader xmlReader;

    internal string Name => this.xmlReader.Name;

    internal string LastName { get; private set; }

    internal AdvancedReader(XmlTextReader xmlReader)
    {
      this.xmlReader = xmlReader;
      this.LastName = string.Empty;
    }

    internal void OmitElement() => this.xmlReader.ReadOuterXml();

    internal string ReadElementString()
    {
      this.LastName = this.xmlReader.Name;
      return this.xmlReader.ReadElementString();
    }

    internal bool FollowToElement()
    {
      if (this.xmlReader.NodeType == XmlNodeType.Element)
        return true;
      while (this.xmlReader.Read())
      {
        if (this.xmlReader.NodeType == XmlNodeType.Element)
          return true;
      }
      return false;
    }

    internal void Skip()
    {
      do
        ;
      while (this.xmlReader.Read() && this.xmlReader.NodeType != XmlNodeType.Element && this.xmlReader.NodeType != XmlNodeType.EndElement);
    }

    internal bool IsEmpty => this.xmlReader.IsEmptyElement;

    internal string ReadElementString(string tagName)
    {
      if (!this.FollowToElement())
        throw OptimaDocument.InvalidFormatException("Nieoczekiwany koniec pliku XML; oczekiwano <{0}>.".Translate(), (object) tagName);
      if (this.xmlReader.Name != tagName)
        throw OptimaDocument.InvalidFormatException("Nieoczekiwany znacznik <{0}>; oczekiwano <{1}>.".Translate(), (object) this.xmlReader.Name, (object) tagName);
      this.LastName = this.xmlReader.Name;
      return this.xmlReader.ReadElementString();
    }

    internal bool OpenOrCloseTag(string[] openTags, string closeTag)
    {
      while (this.xmlReader.NodeType != XmlNodeType.Element && this.xmlReader.NodeType != XmlNodeType.EndElement)
      {
        if (!this.xmlReader.Read())
          throw OptimaDocument.InvalidFormatException("Nieoczekiwany koniec pliku XML; oczekiwano {0} lub </{1}>.".Translate(), (object) this.GetFormattedTags(openTags), (object) closeTag);
      }
      if (this.xmlReader.NodeType == XmlNodeType.Element && ((IEnumerable<string>) openTags).Contains<string>(this.xmlReader.Name))
        return true;
      if (this.xmlReader.NodeType == XmlNodeType.EndElement && this.xmlReader.Name == closeTag)
      {
        this.xmlReader.Read();
        return false;
      }
      throw OptimaDocument.InvalidFormatException("Nieoczekiwany znacznik (lub zamknięcie) {2}; oczekiwano {0} lub </{1}>.".Translate(), (object) this.GetFormattedTags(openTags), (object) closeTag, (object) this.xmlReader.Name);
    }

    internal bool OpenOrCloseTag(string closeTag)
    {
      while (this.xmlReader.NodeType != XmlNodeType.Element && this.xmlReader.NodeType != XmlNodeType.EndElement)
      {
        if (!this.xmlReader.Read())
          throw OptimaDocument.InvalidFormatException("Nieoczekiwany koniec pliku XML; oczekiwano znacznika otwierającego lub </{0}>.".Translate(), (object) closeTag);
      }
      if (this.xmlReader.NodeType == XmlNodeType.Element)
        return true;
      if (this.xmlReader.NodeType == XmlNodeType.EndElement && this.xmlReader.Name == closeTag)
      {
        this.xmlReader.Read();
        return false;
      }
      throw OptimaDocument.InvalidFormatException("Nieoczekiwany znacznik </{0}>; oczekiwano </{1}>.".Translate(), (object) this.xmlReader.Name, (object) closeTag);
    }

    internal bool OmitEmptyElement(string[] tagNames)
    {
      if (!((IEnumerable<string>) tagNames).Contains<string>(this.xmlReader.Name))
        throw OptimaDocument.InvalidFormatException("Oczekiwano znaczników {0}".Translate(), (object) this.GetFormattedTags(tagNames));
      if (this.IsEmpty)
      {
        this.Skip();
        return true;
      }
      this.Skip();
      if (this.xmlReader.NodeType != XmlNodeType.EndElement || !((IEnumerable<string>) tagNames).Contains<string>(this.xmlReader.Name))
        return false;
      this.Skip();
      return true;
    }

    private string GetFormattedTags(string[] tagNames)
    {
      StringBuilder stringBuilder = new StringBuilder();
      foreach (string tagName in tagNames)
        stringBuilder.Append($"<{tagName}>, ");
      stringBuilder.Length -= 3;
      return stringBuilder.ToString();
    }
  }

  private sealed class Parser
  {
    private OptimaDocument Root;

    internal Parser(OptimaDocument document) => this.Root = document;

    private Exception ParseFailed(string value, Type dataType, Exception ex)
    {
      return (Exception) new ApplicationException("Wartość ({0}) w węźle <{1}> jest niepoprawna dla spodziewanego typu danych {2}.".TranslateFormat((object) value, (object) this.Root.AdvReader.LastName, (object) dataType.FullName), ex);
    }

    internal float ParseToFloat() => this.ParseToFloat(this.Root.AdvReader.ReadElementString());

    internal float ParseToFloat(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? 0.0f : float.Parse(value, (IFormatProvider) CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (float), ex);
      }
    }

    internal bool ParseToBool() => this.ParseToBool(this.Root.AdvReader.ReadElementString());

    internal bool ParseToBool(string value)
    {
      return string.Compare(value, "Tak".TranslateIgnore(), true) == 0;
    }

    internal Soneta.Types.Date ParseToDate()
    {
      return this.ParseToDate(this.Root.AdvReader.ReadElementString());
    }

    internal Soneta.Types.Date ParseToDate(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? Soneta.Types.Date.Empty : Soneta.Types.Date.Parse(value, (IFormatProvider) CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (Soneta.Types.Date), ex);
      }
    }

    internal System.Guid ParseToGuid() => this.ParseToGuid(this.Root.AdvReader.ReadElementString());

    internal System.Guid ParseToGuid(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? System.Guid.Empty : new System.Guid(value);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (System.Guid), ex);
      }
    }

    internal System.Decimal ParseToDecimal()
    {
      return this.ParseToDecimal(this.Root.AdvReader.ReadElementString());
    }

    internal System.Decimal ParseToDecimal(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? 0M : System.Decimal.Parse(value, (IFormatProvider) CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (System.Decimal), ex);
      }
    }

    internal double ParseToDouble() => this.ParseToDouble(this.Root.AdvReader.ReadElementString());

    internal double ParseToDouble(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? 0.0 : double.Parse(value, (IFormatProvider) CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (double), ex);
      }
    }

    internal int ParseToInt() => this.ParseToInt(this.Root.AdvReader.ReadElementString());

    internal int ParseToInt(string value)
    {
      try
      {
        return string.IsNullOrEmpty(value) ? 0 : int.Parse(value, (IFormatProvider) CultureInfo.InvariantCulture);
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (int), ex);
      }
    }

    internal Wojewodztwa ParseToWojewodztwa()
    {
      return this.ParseToWojewodztwa(this.Root.AdvReader.ReadElementString());
    }

    internal Wojewodztwa ParseToWojewodztwa(string value)
    {
      switch (value)
      {
        case "":
          return Wojewodztwa.nieokreślone;
        case "kujawsko-pomorskie":
          return Wojewodztwa.kujawsko_pomorskie;
        case "warmińsko-mazurskie":
          return Wojewodztwa.warmińsko_mazurskie;
        default:
          try
          {
            return (Wojewodztwa) System.Enum.Parse(typeof (Wojewodztwa), value, true);
          }
          catch (Exception ex)
          {
            return Wojewodztwa.nieokreślone;
          }
      }
    }

    internal RodzajPodmiotu? ParseToRodzajPodmiotu()
    {
      return this.ParseToRodzajPodmiotu(this.Root.AdvReader.ReadElementString());
    }

    internal RodzajPodmiotu? ParseToRodzajPodmiotu(string value)
    {
      if (string.IsNullOrEmpty(value))
        return new RodzajPodmiotu?();
      try
      {
        return new RodzajPodmiotu?((RodzajPodmiotu) System.Enum.Parse(typeof (RodzajPodmiotu), value, true));
      }
      catch (Exception ex)
      {
        throw this.ParseFailed(value, typeof (RodzajPodmiotu), ex);
      }
    }
  }

  internal abstract class VersionedItem
  {
    protected readonly OptimaDocument Root;
    private float fmtVersion;

    protected float FmtVersion
    {
      get
      {
        return (double) this.fmtVersion != 0.0 ? this.fmtVersion : throw new ApplicationException("Nie określono wersji formatu dla obiektu typu {0}.".TranslateFormat((object) this.GetType().FullName));
      }
      set => this.fmtVersion = value;
    }

    protected VersionedItem(OptimaDocument optimaDocument)
    {
      this.Root = optimaDocument;
      this.FmtVersion = 0.0f;
    }
  }

  internal abstract class Item(OptimaDocument document) : OptimaDocument.VersionedItem(document)
  {
    internal virtual void Initialize()
    {
      while (this.Root.AdvReader.OpenOrCloseTag(this.Name))
      {
        if (!this.InitializeTag(this.Root.AdvReader.Name))
          this.Root.AdvReader.OmitElement();
      }
    }

    protected abstract bool InitializeTag(string tagName);

    internal abstract string Name { get; }

    internal virtual Row Save()
    {
      throw new NotImplementedException("Save of ".TranslateIgnore() + this.GetType().Name);
    }
  }

  internal abstract class MainItem : OptimaDocument.Item
  {
    internal System.Guid ID_ZRODLA;
    protected bool IdentifiedByGuid;

    protected virtual string ID_ZRODLA_TAGNAME => "ID_ZRODLA";

    internal virtual string IDENT_CODE => string.Empty;

    protected MainItem(OptimaDocument document, float fmtVersion)
      : base(document)
    {
      this.FmtVersion = fmtVersion;
      this.IdentifiedByGuid = true;
    }

    internal sealed override void Initialize()
    {
      if (this.IdentifiedByGuid)
        this.ID_ZRODLA = this.Root.AdvParser.ParseToGuid(this.Root.AdvReader.ReadElementString(this.ID_ZRODLA_TAGNAME));
      base.Initialize();
    }
  }

  internal abstract class BaseProxy
  {
    protected readonly OptimaDocument Root;

    protected BaseProxy(OptimaDocument document) => this.Root = document;

    internal abstract bool AcceptTag(string tagName);
  }

  internal abstract class ItemProxy : OptimaDocument.BaseProxy
  {
    private readonly string identCode;
    private readonly string identGuid;

    internal string Kod { get; private set; }

    internal System.Guid ID { get; private set; }

    protected ItemProxy(OptimaDocument document, string name)
      : this(name, name + "_ID", document)
    {
    }

    protected ItemProxy(OptimaDocument document, string name, string suffix)
      : this($"{name}_{suffix}", $"{name}_ID_{suffix}", document)
    {
    }

    protected ItemProxy(string identCode, string identGuid, OptimaDocument document)
      : base(document)
    {
      this.Kod = string.Empty;
      this.ID = System.Guid.Empty;
      this.identCode = identCode;
      this.identGuid = identGuid;
    }

    internal override bool AcceptTag(string tagName)
    {
      if (tagName == this.identCode)
      {
        this.Kod = this.Root.AdvReader.ReadElementString();
        return true;
      }
      if (!(tagName == this.identGuid))
        return false;
      this.ID = this.Root.AdvParser.ParseToGuid();
      return true;
    }
  }

  internal abstract class ItemsCollection : ReadOnlyCollectionBase
  {
    private float fmtVersion;
    protected readonly OptimaDocument Root;

    protected float FmtVersion
    {
      get
      {
        return (double) this.fmtVersion != 0.0 ? this.fmtVersion : throw new ApplicationException("Nie określono wersji formatu dla obiektu typu {0}.".TranslateFormat((object) this.GetType().FullName));
      }
      set => this.fmtVersion = value;
    }

    protected abstract string Name { get; }

    protected abstract string[] SubNames { get; }

    protected abstract OptimaDocument.Item CreateItem(string name);

    protected ItemsCollection(OptimaDocument document)
    {
      this.Root = document;
      this.FmtVersion = 0.0f;
    }

    internal OptimaDocument.Item this[int index] => (OptimaDocument.Item) this.InnerList[index];

    protected virtual void PreReadCollection()
    {
    }

    protected virtual void PostReadCollection()
    {
    }

    internal bool InitializeCollection()
    {
      if (this.Root.AdvReader.OmitEmptyElement(new string[1]
      {
        this.Name
      }))
        return true;
      this.PreReadCollection();
      this.ReadCollection();
      this.PostReadCollection();
      return true;
    }

    private void ReadCollection()
    {
      while (this.Root.AdvReader.OpenOrCloseTag(this.SubNames, this.Name))
      {
        string name = this.Root.AdvReader.Name;
        if (!this.Root.AdvReader.OmitEmptyElement(this.SubNames))
        {
          OptimaDocument.Item obj = this.CreateItem(name);
          obj.Initialize();
          this.InnerList.Add((object) obj);
        }
      }
    }
  }

  internal abstract class MainItemsCollection(OptimaDocument document) : 
    OptimaDocument.ItemsCollection(document)
  {
    protected float WERSJA;
    private Dictionary<System.Guid, OptimaDocument.MainItem> itemsByGuid = new Dictionary<System.Guid, OptimaDocument.MainItem>();
    private Dictionary<string, OptimaDocument.MainItem> itemsByCode = new Dictionary<string, OptimaDocument.MainItem>();

    internal OptimaDocument.MainItem this[int index]
    {
      get => (OptimaDocument.MainItem) this.InnerList[index];
    }

    internal OptimaDocument.MainItem this[System.Guid guid]
    {
      get
      {
        return !this.itemsByGuid.ContainsKey(guid) ? (OptimaDocument.MainItem) null : this.itemsByGuid[guid];
      }
    }

    internal OptimaDocument.MainItem ByCode(string code)
    {
      return !this.itemsByCode.ContainsKey(code) ? (OptimaDocument.MainItem) null : this.itemsByCode[code];
    }

    protected override void PreReadCollection()
    {
      this.WERSJA = this.Root.AdvParser.ParseToFloat(this.Root.AdvReader.ReadElementString("WERSJA"));
      if ((double) this.WERSJA != 1.0 && (double) this.WERSJA != 2.0)
        throw new ApplicationException("Nieobsługiwana wersja formatu Opt!ma ({0}).".TranslateFormat((object) this.WERSJA));
      this.Root.AdvReader.ReadElementString("BAZA_ZRD_ID");
      this.Root.AdvReader.ReadElementString("BAZA_DOC_ID");
    }

    protected override void PostReadCollection()
    {
      foreach (OptimaDocument.MainItem inner in this.InnerList)
      {
        System.Guid idZrodla = inner.ID_ZRODLA;
        string identCode = inner.IDENT_CODE;
        if (idZrodla != System.Guid.Empty)
          this.itemsByGuid[idZrodla] = inner;
        if (!string.IsNullOrEmpty(identCode))
          this.itemsByCode[identCode] = inner;
      }
    }
  }

  internal sealed class Rachunki : OptimaDocument.ItemsCollection
  {
    internal const string PrivateName = "RACHUNKI";

    protected override string Name => "RACHUNKI";

    protected override string[] SubNames
    {
      get => new string[1]{ "RACHUNEK" };
    }

    internal Rachunki(OptimaDocument document)
      : base(document)
    {
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Rachunek(this.Root);
    }
  }

  internal sealed class Rachunek : OptimaDocument.Item
  {
    internal const string PrivateName = "RACHUNEK";
    internal string TYP;
    internal string NR_RACHUNKU;
    internal bool IBAN;

    internal Rachunek(OptimaDocument document)
      : base(document)
    {
    }

    internal override string Name => "RACHUNEK";

    protected override bool InitializeTag(string tagName)
    {
      switch (tagName)
      {
        case "TYP":
          this.TYP = this.Root.AdvReader.ReadElementString();
          return true;
        case "NR_RACHUNKU":
          this.NR_RACHUNKU = this.Root.AdvReader.ReadElementString();
          return true;
        case "IBAN":
          this.IBAN = this.Root.AdvParser.ParseToBool();
          return true;
        default:
          return false;
      }
    }
  }

  internal sealed class RachunekProxy : OptimaDocument.ItemProxy
  {
    internal const string PrivateName = "RACHUNEK";

    internal RachunekProxy(OptimaDocument document)
      : base(document, "RACHUNEK")
    {
    }
  }

  internal sealed class Adresy : OptimaDocument.ItemsCollection
  {
    internal const string PrivateName = "ADRESY";

    protected override string Name => "ADRESY";

    protected override string[] SubNames
    {
      get
      {
        return new string[2]
        {
          "ADRES",
          "ADRES_KORESPONDENCYJNY"
        };
      }
    }

    internal Adresy(OptimaDocument document)
      : base(document)
    {
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return name == "ADRES_KORESPONDENCYJNY" ? (OptimaDocument.Item) new OptimaDocument.AdresKorespondencyjny(this.Root) : (OptimaDocument.Item) new OptimaDocument.Adres(this.Root);
    }
  }

  internal sealed class AdresKorespondencyjny : OptimaDocument.Adres
  {
    internal new const string PrivateName = "ADRES_KORESPONDENCYJNY";

    internal AdresKorespondencyjny(OptimaDocument document)
      : base(document)
    {
    }

    internal override string Name => "ADRES_KORESPONDENCYJNY";
  }

  internal class Adres : OptimaDocument.Item
  {
    internal const string PrivateName = "ADRES";
    internal string AKRONIM = string.Empty;
    internal string STATUS = string.Empty;
    internal string KRAJ = string.Empty;
    internal string POWIAT = string.Empty;
    internal string GMINA = string.Empty;
    internal string ULICA = string.Empty;
    internal string NR_DOMU = string.Empty;
    internal string NR_LOKALU = string.Empty;
    internal string MIASTO = string.Empty;
    internal string KOD_POCZTOWY = string.Empty;
    internal string POCZTA = string.Empty;
    internal string NIP_KRAJ = string.Empty;
    internal string NIP = string.Empty;
    internal string NAZWA1 = string.Empty;
    internal string NAZWA2 = string.Empty;
    internal string NAZWA3 = string.Empty;
    internal string TELEFON1 = string.Empty;
    internal string TELEFON2 = string.Empty;
    internal string FAX = string.Empty;
    internal string URL = string.Empty;
    internal string EMAIL = string.Empty;
    internal Wojewodztwa WOJEWODZTWO;

    internal Adres(OptimaDocument document)
      : base(document)
    {
    }

    internal override string Name => "ADRES";

    protected override bool InitializeTag(string tagName)
    {
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 3:
            switch (tagName[0])
            {
              case 'F':
                if (tagName == "FAX")
                {
                  this.FAX = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'N':
                if (tagName == "NIP")
                {
                  this.NIP = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'U':
                if (tagName == "URL")
                {
                  this.URL = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 4:
            if (tagName == "KRAJ")
            {
              this.KRAJ = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 5:
            switch (tagName[0])
            {
              case 'E':
                if (tagName == "EMAIL")
                {
                  this.EMAIL = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'G':
                if (tagName == "GMINA")
                {
                  this.GMINA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'U':
                if (tagName == "ULICA")
                {
                  this.ULICA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 6:
            switch (tagName[5])
            {
              case '1':
                if (tagName == "NAZWA1")
                {
                  this.NAZWA1 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case '2':
                if (tagName == "NAZWA2")
                {
                  this.NAZWA2 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case '3':
                if (tagName == "NAZWA3")
                {
                  this.NAZWA3 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'A':
                if (tagName == "POCZTA")
                {
                  this.POCZTA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'O':
                if (tagName == "MIASTO")
                {
                  this.MIASTO = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'S':
                if (tagName == "STATUS")
                {
                  this.STATUS = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'T':
                if (tagName == "POWIAT")
                {
                  this.POWIAT = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 7:
            switch (tagName[0])
            {
              case 'A':
                if (tagName == "AKRONIM")
                {
                  this.AKRONIM = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'N':
                if (tagName == "NR_DOMU")
                {
                  this.NR_DOMU = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 8:
            switch (tagName[7])
            {
              case '1':
                if (tagName == "TELEFON1")
                {
                  this.TELEFON1 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case '2':
                if (tagName == "TELEFON2")
                {
                  this.TELEFON2 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'J':
                if (tagName == "NIP_KRAJ")
                {
                  this.NIP_KRAJ = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 9:
            if (tagName == "NR_LOKALU")
            {
              this.NR_LOKALU = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 11:
            if (tagName == "WOJEWODZTWO")
            {
              this.WOJEWODZTWO = this.Root.AdvParser.ParseToWojewodztwa();
              return true;
            }
            break;
          case 12:
            if (tagName == "KOD_POCZTOWY")
            {
              this.KOD_POCZTOWY = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
        }
      }
      return false;
    }

    internal void CopyTo(Soneta.Core.Adres adres)
    {
      adres.Faks = this.FAX;
      adres.Gmina = this.GMINA;
      adres.KodPocztowyS = this.KOD_POCZTOWY;
      adres.Kraj = this.KRAJ;
      adres.Miejscowosc = Soneta.Core.Tools.Left(this.MIASTO, 56);
      adres.Poczta = this.POCZTA;
      adres.Powiat = this.POWIAT;
      adres.Telefon = this.TELEFON1;
      adres.ParseUlicaNumer(this.ULICA);
      if (!string.IsNullOrEmpty(this.NR_DOMU))
        adres.NrDomu = this.NR_DOMU;
      if (!string.IsNullOrEmpty(this.NR_LOKALU))
        adres.NrLokalu = this.NR_LOKALU;
      adres.Wojewodztwo = this.WOJEWODZTWO;
    }

    internal void CopyTo(Kontakt kontakt)
    {
      kontakt.EMAIL = this.EMAIL;
      kontakt.TelefonKomorkowy = this.TELEFON2;
      kontakt.WWW = this.URL;
    }
  }

  public class FormaPlatnosciProxy : OptimaDocument.ItemProxy
  {
    internal FormaPlatnosciProxy(OptimaDocument document)
      : base(document, "FORMA_PLATNOSCI")
    {
    }

    internal FormaPlatnosciProxy(OptimaDocument document, string suffix)
      : base(document, "FORMA_PLATNOSCI", suffix)
    {
    }
  }

  [TranslateIgnore]
  internal sealed class PodmiotProxy : OptimaDocument.ItemProxy
  {
    private bool IdentifiedByGuid;

    internal string Typ { get; private set; }

    internal PodmiotProxy(OptimaDocument document)
      : base(document, "PODMIOT")
    {
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
    }

    internal override bool AcceptTag(string tagName)
    {
      if (base.AcceptTag(tagName))
        return true;
      if (!(tagName == "TYP_PODMIOTU"))
        return false;
      this.Typ = this.Root.AdvReader.ReadElementString();
      return true;
    }

    private OptimaDocument.MainItem GetMainItemById()
    {
      switch (this.Typ)
      {
        case "kontrahent":
          return this.Root.CtnKontrahenci == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnKontrahenci[this.ID];
        case "bank":
          return this.Root.CtnBanki == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnBanki[this.ID];
        case "pracownik":
          return this.Root.CtnPracownicy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnPracownicy[this.ID];
        case "wspólnik":
          return this.Root.CtnWspolnicy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnWspolnicy[this.ID];
        case "urząd":
          return this.Root.CtnUrzedy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnUrzedy[this.ID];
        default:
          return (OptimaDocument.MainItem) null;
      }
    }

    private OptimaDocument.MainItem GetMainItemByCode()
    {
      switch (this.Typ)
      {
        case "kontrahent":
          return this.Root.CtnKontrahenci == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnKontrahenci.ByCode(this.Kod);
        case "bank":
          return this.Root.CtnBanki == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnBanki.ByCode(this.Kod);
        case "pracownik":
          return this.Root.CtnPracownicy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnPracownicy.ByCode(this.Kod);
        case "wspólnik":
          return this.Root.CtnWspolnicy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnWspolnicy.ByCode(this.Kod);
        case "urząd":
          return this.Root.CtnUrzedy == null ? (OptimaDocument.MainItem) null : (OptimaDocument.MainItem) this.Root.CtnUrzedy.ByCode(this.Kod);
        default:
          return (OptimaDocument.MainItem) null;
      }
    }

    private Row FindRowById()
    {
      if (this.Typ == "kontrahent" && this.Kod == "!NIEOKREŚLONY!")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Kontrahenci.Incydentalny;
      if (this.Typ == "ZUS")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY.ZUSCentrala;
      if (this.Typ == "urząd")
      {
        GuidedTable zusy = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY;
        if (zusy.Contains(this.ID))
          return (Row) zusy[this.ID];
        GuidedTable urzedySkarbowe = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedySkarbowe;
        if (urzedySkarbowe.Contains(this.ID))
          return (Row) urzedySkarbowe[this.ID];
        GuidedTable urzedyCelne = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedyCelne;
        return urzedyCelne.Contains(this.ID) ? (Row) urzedyCelne[this.ID] : (Row) null;
      }
      GuidedTable guidedTable;
      switch (this.Typ)
      {
        case "kontrahent":
          guidedTable = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Kontrahenci;
          break;
        case "bank":
          guidedTable = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Banki;
          break;
        case "pracownik":
        case "wspólnik":
          guidedTable = (GuidedTable) KadryModule.GetInstance((ISessionable) this.Root.EnovaSession).Pracownicy;
          break;
        default:
          throw new Exception("Nieznany typ podmiotu '{0}'".TranslateFormat((object) this.Typ));
      }
      return !guidedTable.Contains(this.ID) ? (Row) null : (Row) guidedTable[this.ID];
    }

    private Row FindRowByCode()
    {
      if (this.Typ == "kontrahent" && this.Kod == "!NIEOKREŚLONY!")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Kontrahenci.Incydentalny;
      if (this.Typ == "ZUS")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY.ZUSCentrala;
      if (this.Typ == "urząd")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY.WgKodu[this.Kod] ?? (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedySkarbowe.WgKodu[this.Kod] ?? (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedyCelne.WgKodu[this.Kod] ?? (Row) null;
      if (this.Typ == "kontrahent")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Kontrahenci.WgKodu[this.Kod];
      if (this.Typ == "bank")
        return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Banki.WgKodu[this.Kod];
      if (this.Typ == "pracownik" || this.Typ == "wspólnik")
        return (Row) KadryModule.GetInstance((ISessionable) this.Root.EnovaSession).Pracownicy.WgKodu[this.Kod];
      throw new Exception("Nieznany typ podmiotu '{0}'".TranslateFormat((object) this.Typ));
    }

    internal IPodmiot Find()
    {
      if (string.IsNullOrEmpty(this.Typ))
        throw new Exception(string.Format(this.IdentifiedByGuid ? "Nieokreślony typ podmiotu '{1}' ({0})".Translate() : "Nieokreślony typ podmiotu '{0}'".Translate(), (object) this.Kod, (object) this.ID));
      if (this.Typ == "ZUS")
        return (IPodmiot) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY.ZUSCentrala;
      IPodmiot podmiot = (IPodmiot) null;
      if (this.IdentifiedByGuid && this.ID != System.Guid.Empty)
      {
        OptimaDocument.MainItem mainItemById = this.GetMainItemById();
        podmiot = mainItemById != null ? (IPodmiot) mainItemById.Save() : (IPodmiot) this.FindRowById();
      }
      else if (!this.IdentifiedByGuid && !string.IsNullOrEmpty(this.Kod))
      {
        OptimaDocument.MainItem mainItemByCode = this.GetMainItemByCode();
        podmiot = mainItemByCode != null ? (IPodmiot) mainItemByCode.Save() : (IPodmiot) this.FindRowByCode();
      }
      return podmiot != null ? Soneta.Core.Tools.Zamiennik(podmiot) : throw new Exception(string.Format(this.IdentifiedByGuid ? "Nie znaleziono podmiotu '{1}' ({0})".Translate() : "Nie znaleziono podmiotu '{0}'".Translate(), (object) this.Kod, (object) this.ID));
    }
  }

  internal sealed class ZapisyKB : OptimaDocument.ItemsCollection
  {
    internal const string PrivateName = "ZAPISY_KB";

    protected override string Name => "ZAPISY_KB";

    protected override string[] SubNames
    {
      get => new string[1]{ "ZAPIS_KB" };
    }

    internal ZapisyKB(OptimaDocument document, float fmtVersion)
      : base(document)
    {
      this.FmtVersion = fmtVersion;
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.ZapisKB(this.Root, this.FmtVersion);
    }
  }

  internal sealed class ZapisKB : OptimaDocument.MainItem
  {
    internal const string PrivateName = "ZAPIS_KB";
    internal Soneta.Types.Date DATA_DOK;
    internal string NUMER;
    internal System.Decimal KWOTA;
    internal string WALUTA;
    internal string KURS_WALUTY;
    internal double NOTOWANIE_WALUTY_ILE;
    internal int NOTOWANIE_WALUTY_ZA_ILE;
    internal System.Decimal KWOTA_PLN;
    internal string KIERUNEK;
    internal bool PODLEGA_ROZLICZENIU;
    internal string OPIS;
    internal bool ZWROT_ENOVA;
    internal string TYP;
    internal OptimaDocument.PodmiotProxy PODMIOT;
    internal OptimaDocument.KwotyDodatkowe KWOTY_DODATKOWE;

    internal ZapisKB(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.PODLEGA_ROZLICZENIU = true;
      this.PODMIOT = new OptimaDocument.PodmiotProxy(document);
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
      this.KWOTY_DODATKOWE = new OptimaDocument.KwotyDodatkowe(document, OptimaDocument.KwotaDodatkowa.TrybPracy.RaportKB);
    }

    protected override string ID_ZRODLA_TAGNAME
    {
      get => (double) this.FmtVersion < 2.0 ? base.ID_ZRODLA_TAGNAME : "ID_ZRODLA_ZAPISU";
    }

    internal override string Name => "ZAPIS_KB";

    protected override bool InitializeTag(string tagName)
    {
      if (this.PODMIOT.AcceptTag(tagName))
        return true;
      switch (tagName)
      {
        case "KWOTY_DODATKOWE":
          return this.KWOTY_DODATKOWE.InitializeCollection();
        case null:
label_35:
          return false;
        default:
          switch (tagName.Length)
          {
            case 3:
              if (tagName == "TYP")
              {
                this.TYP = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_35;
            case 4:
              if (tagName == "OPIS")
              {
                this.OPIS = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_35;
            case 5:
              switch (tagName[0])
              {
                case 'K':
                  if (tagName == "KWOTA")
                  {
                    this.KWOTA = this.Root.AdvParser.ParseToDecimal();
                    return true;
                  }
                  goto label_35;
                case 'N':
                  if (tagName == "NUMER")
                    break;
                  goto label_35;
                default:
                  goto label_35;
              }
              break;
            case 6:
              if (tagName == "WALUTA")
              {
                this.WALUTA = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_35;
            case 8:
              switch (tagName[0])
              {
                case 'D':
                  if (tagName == "DATA_DOK")
                  {
                    this.DATA_DOK = this.Root.AdvParser.ParseToDate();
                    return true;
                  }
                  goto label_35;
                case 'K':
                  if (tagName == "KIERUNEK")
                  {
                    this.KIERUNEK = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_35;
                default:
                  goto label_35;
              }
            case 9:
              if (tagName == "KWOTA_PLN")
              {
                this.KWOTA_PLN = this.Root.AdvParser.ParseToDecimal();
                return true;
              }
              goto label_35;
            case 11:
              switch (tagName[0])
              {
                case 'K':
                  if (tagName == "KURS_WALUTY")
                  {
                    this.KURS_WALUTY = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_35;
                case 'Z':
                  if (tagName == "ZWROT_ENOVA")
                  {
                    this.ZWROT_ENOVA = this.Root.AdvParser.ParseToBool();
                    return true;
                  }
                  goto label_35;
                default:
                  goto label_35;
              }
            case 12:
              if (tagName == "NUMER_ZAPISU")
                break;
              goto label_35;
            case 19:
              if (tagName == "PODLEGA_ROZLICZENIU")
              {
                this.PODLEGA_ROZLICZENIU = this.Root.AdvParser.ParseToBool();
                return true;
              }
              goto label_35;
            case 20:
              if (tagName == "NOTOWANIE_WALUTY_ILE")
              {
                this.NOTOWANIE_WALUTY_ILE = this.Root.AdvParser.ParseToDouble();
                return true;
              }
              goto label_35;
            case 23:
              if (tagName == "NOTOWANIE_WALUTY_ZA_ILE")
              {
                this.NOTOWANIE_WALUTY_ZA_ILE = this.Root.AdvParser.ParseToInt();
                return true;
              }
              goto label_35;
            default:
              goto label_35;
          }
          this.NUMER = this.Root.AdvReader.ReadElementString();
          return true;
      }
    }

    [TranslateIgnore]
    internal void Save(RaportESPEwidencja ewidencja)
    {
      Zaplata zrodloOpis;
      switch (string.IsNullOrEmpty(this.KIERUNEK) ? this.KIERUNEK : this.KIERUNEK.ToLower())
      {
        case "przychód":
        case "przychod":
          zrodloOpis = (Zaplata) new WplataEwidencja(ewidencja);
          break;
        case "rozchód":
        case "rozchod":
          zrodloOpis = (Zaplata) new WyplataEwidencja(ewidencja);
          break;
        default:
          throw new Exception("Nieznany kierunek przepływu pieniędzy '{0}'".TranslateFormat((object) this.KIERUNEK));
      }
      KasaModule instance = KasaModule.GetInstance((ISessionable) this.Root.EnovaSession);
      instance.Zaplaty.AddRow((Row) zrodloOpis);
      zrodloOpis.DataDokumentu = this.DATA_DOK;
      if (!zrodloOpis.IsReadOnlyNumerDokumentu())
        zrodloOpis.NumerDokumentu = this.NUMER;
      if (this.PODMIOT.Typ != "")
      {
        zrodloOpis.Podmiot = (IPodmiotKasowy) this.PODMIOT.Find();
        if (!this.PODLEGA_ROZLICZENIU)
          zrodloOpis.Rozliczana = false;
      }
      else
        zrodloOpis.Rozliczana = false;
      if (!string.IsNullOrEmpty(this.WALUTA) && this.WALUTA != "PLN")
      {
        zrodloOpis.Kwota = new Currency(this.KWOTA, this.WALUTA);
        TabelaKursowa tabelaKursowa = WalutyModule.GetInstance((ISessionable) this.Root.EnovaSession).TabeleKursowe.WgNazwy[this.KURS_WALUTY];
        if (tabelaKursowa != null)
          zrodloOpis.TabelaKursowa = tabelaKursowa;
        zrodloOpis.Kurs = this.NOTOWANIE_WALUTY_ILE * (double) this.NOTOWANIE_WALUTY_ZA_ILE;
        zrodloOpis.WartoscWgKursu = new Currency(this.KWOTA_PLN);
      }
      else
        zrodloOpis.Kwota = new Currency(this.KWOTA);
      if (!string.IsNullOrEmpty(this.TYP))
      {
        SposobZaplaty sposobZaplaty = instance.SposobyZaplaty.WgNazwy[this.TYP];
        if (sposobZaplaty != null && zrodloOpis.SposobZaplaty != sposobZaplaty)
          zrodloOpis.SposobZaplaty = sposobZaplaty;
        else if (sposobZaplaty == null)
          this.Root.importLog.WriteLine("Dla zapłaty numer '{0}' na kwotę '{1}' nie ustawiono sposobu zapłaty '{2}'.".TranslateFormat((object) zrodloOpis.NumerDokumentu, (object) zrodloOpis.Kwota, (object) this.TYP));
      }
      zrodloOpis.Zwrot = this.ZWROT_ENOVA;
      zrodloOpis.Opis = !string.IsNullOrEmpty(this.OPIS) ? this.OPIS : "Brak".TranslateIgnore();
      if (!this.Root.SettingSaveOA)
        return;
      this.KWOTY_DODATKOWE.Save((IZrodloOpisuAnalitycznego) zrodloOpis, (DokEwidencji) null);
    }
  }

  internal sealed class Platnosci : OptimaDocument.ItemsCollection
  {
    internal const string PrivateName = "PLATNOSCI";

    protected override string Name => "PLATNOSCI";

    protected override string[] SubNames
    {
      get => new string[1]{ "PLATNOSC" };
    }

    internal Platnosci(OptimaDocument document, float fmtVersion)
      : base(document)
    {
      this.FmtVersion = fmtVersion;
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Platnosc(this.Root, this.FmtVersion);
    }
  }

  internal sealed class Platnosc : OptimaDocument.Item
  {
    internal const string PrivateName = "PLATNOSC";
    internal Soneta.Types.Date TERMIN;
    internal System.Decimal KWOTA;
    internal string WALUTA;
    internal string KURS_WALUTY;
    internal double NOTOWANIE_WALUTY_ILE;
    internal int NOTOWANIE_WALUTY_ZA_ILE;
    internal System.Decimal KWOTA_PLN;
    internal string KIERUNEK;
    internal OptimaDocument.FormaPlatnosciProxy FORMA_PLATNOSCI;
    internal OptimaDocument.PodmiotProxy PODMIOT;

    internal Platnosc(OptimaDocument document, float fmtVersion)
      : base(document)
    {
      this.FmtVersion = fmtVersion;
      this.PODMIOT = new OptimaDocument.PodmiotProxy(this.Root);
      this.FORMA_PLATNOSCI = (double) fmtVersion >= 2.0 ? new OptimaDocument.FormaPlatnosciProxy(this.Root, "PLAT") : new OptimaDocument.FormaPlatnosciProxy(this.Root);
    }

    internal override string Name => "PLATNOSC";

    protected override bool InitializeTag(string tagName)
    {
      if (this.PODMIOT.AcceptTag(tagName) || this.FORMA_PLATNOSCI.AcceptTag(tagName))
        return true;
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 5:
            if (tagName == "KWOTA")
              goto label_22;
            goto label_29;
          case 6:
            switch (tagName[0])
            {
              case 'T':
                if (tagName == "TERMIN")
                  break;
                goto label_29;
              case 'W':
                if (tagName == "WALUTA")
                  goto label_23;
                goto label_29;
              default:
                goto label_29;
            }
            break;
          case 8:
            if (tagName == "KIERUNEK")
            {
              this.KIERUNEK = this.Root.AdvReader.ReadElementString();
              return true;
            }
            goto label_29;
          case 9:
            if (tagName == "KWOTA_PLN")
              goto label_25;
            goto label_29;
          case 10:
            if (tagName == "KWOTA_PLAT")
              goto label_22;
            goto label_29;
          case 11:
            switch (tagName[0])
            {
              case 'K':
                if (tagName == "KURS_WALUTY")
                  goto label_24;
                goto label_29;
              case 'T':
                if (tagName == "TERMIN_PLAT")
                  break;
                goto label_29;
              case 'W':
                if (tagName == "WALUTA_PLAT")
                  goto label_23;
                goto label_29;
              default:
                goto label_29;
            }
            break;
          case 14:
            if (tagName == "KWOTA_PLN_PLAT")
              goto label_25;
            goto label_29;
          case 16 /*0x10*/:
            if (tagName == "KURS_WALUTY_PLAT")
              goto label_24;
            goto label_29;
          case 20:
            if (tagName == "NOTOWANIE_WALUTY_ILE")
              goto label_27;
            goto label_29;
          case 23:
            if (tagName == "NOTOWANIE_WALUTY_ZA_ILE")
              goto label_28;
            goto label_29;
          case 25:
            if (tagName == "NOTOWANIE_WALUTY_ILE_PLAT")
              goto label_27;
            goto label_29;
          case 28:
            if (tagName == "NOTOWANIE_WALUTY_ZA_ILE_PLAT")
              goto label_28;
            goto label_29;
          default:
            goto label_29;
        }
        this.TERMIN = this.Root.AdvParser.ParseToDate();
        return true;
label_22:
        this.KWOTA = this.Root.AdvParser.ParseToDecimal();
        return true;
label_23:
        this.WALUTA = this.Root.AdvReader.ReadElementString();
        return true;
label_24:
        this.KURS_WALUTY = this.Root.AdvReader.ReadElementString();
        return true;
label_25:
        this.KWOTA_PLN = this.Root.AdvParser.ParseToDecimal();
        return true;
label_27:
        this.NOTOWANIE_WALUTY_ILE = this.Root.AdvParser.ParseToDouble();
        return true;
label_28:
        this.NOTOWANIE_WALUTY_ZA_ILE = this.Root.AdvParser.ParseToInt();
        return true;
      }
label_29:
      return false;
    }
  }

  internal sealed class PozycjeRejestruVAT : OptimaDocument.ItemsCollection
  {
    internal const string PrivateName = "POZYCJE";

    internal PozycjeRejestruVAT(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "POZYCJE";

    protected override string[] SubNames
    {
      get => new string[1]{ "POZYCJA" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.PozycjaRejestruVAT(this.Root);
    }
  }

  internal sealed class PozycjaRejestruVAT : OptimaDocument.Item
  {
    internal const string PrivateName = "POZYCJA";
    internal System.Decimal STAWKA_VAT;
    internal string STATUS_VAT;
    internal System.Decimal NETTO;
    internal System.Decimal VAT;
    internal System.Decimal NETTO_SYS;
    internal System.Decimal VAT_SYS;
    internal string RODZAJ_ZAKUPU;
    internal string RODZAJ_SPRZEDAZY;
    internal string ODLICZENIA_VAT;
    internal string KOLUMNA_KPR;
    internal string OPIS_POZ;

    internal PozycjaRejestruVAT(OptimaDocument document)
      : base(document)
    {
    }

    internal override string Name => "POZYCJA";

    protected override bool InitializeTag(string tagName)
    {
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 3:
            if (tagName == "VAT")
            {
              this.VAT = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
          case 5:
            if (tagName == "NETTO")
            {
              this.NETTO = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
          case 7:
            if (tagName == "VAT_SYS")
            {
              this.VAT_SYS = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
          case 8:
            if (tagName == "OPIS_POZ")
            {
              this.OPIS_POZ = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 9:
            if (tagName == "NETTO_SYS")
            {
              this.NETTO_SYS = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
          case 10:
            switch (tagName[3])
            {
              case 'T':
                if (tagName == "STATUS_VAT")
                {
                  this.STATUS_VAT = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'W':
                if (tagName == "STAWKA_VAT")
                {
                  this.STAWKA_VAT = this.Root.AdvParser.ParseToDecimal();
                  return true;
                }
                break;
            }
            break;
          case 11:
            if (tagName == "KOLUMNA_KPR")
            {
              this.KOLUMNA_KPR = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 13:
            if (tagName == "RODZAJ_ZAKUPU")
            {
              this.RODZAJ_ZAKUPU = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 14:
            if (tagName == "ODLICZENIA_VAT")
            {
              this.ODLICZENIA_VAT = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 16 /*0x10*/:
            if (tagName == "RODZAJ_SPRZEDAZY")
            {
              this.RODZAJ_SPRZEDAZY = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
        }
      }
      return false;
    }
  }

  internal sealed class KwotyDodatkowe : OptimaDocument.ItemsCollection
  {
    private readonly OptimaDocument.KwotaDodatkowa.TrybPracy trybPracy;
    internal const string PrivateName = "KWOTY_DODATKOWE";

    internal KwotyDodatkowe(
      OptimaDocument document,
      OptimaDocument.KwotaDodatkowa.TrybPracy trybPracy)
      : base(document)
    {
      this.trybPracy = trybPracy;
    }

    protected override string Name => "KWOTY_DODATKOWE";

    protected override string[] SubNames
    {
      get
      {
        return this.trybPracy != OptimaDocument.KwotaDodatkowa.TrybPracy.RejestrVAT ? new string[1]
        {
          "POZYCJA"
        } : new string[1]{ "POZYCJA_KD" };
      }
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.KwotaDodatkowa(this.Root, this.trybPracy);
    }

    internal void Save(IZrodloOpisuAnalitycznego zrodloOpis, DokEwidencji dokEwidencji)
    {
      int num = 0;
      Currency currency;
      if (dokEwidencji != null)
      {
        currency = dokEwidencji.Wartosc;
        num = System.Math.Sign(currency.Value);
      }
      if (zrodloOpis != null)
      {
        currency = zrodloOpis.KwotaPodstawy("");
        num = System.Math.Sign(currency.Value);
      }
      foreach (OptimaDocument.KwotaDodatkowa kd in (ReadOnlyCollectionBase) this)
      {
        if ((num == 0 || System.Math.Sign(kd.KWOTA_KD_SYS) == num) && (!string.IsNullOrEmpty(kd.KONTO_MA) || !string.IsNullOrEmpty(kd.KONTO_WN)))
        {
          if (!string.IsNullOrEmpty(kd.KONTO_MA))
          {
            ElemOpisuAnalitycznego elem = zrodloOpis != null ? (ElemOpisuAnalitycznego) new ElementOpisuZrodla(zrodloOpis) : (ElemOpisuAnalitycznego) new ElementOpisuEwidencji(dokEwidencji);
            KsiegaModule.GetInstance((ISessionable) this.Root.EnovaSession).OpisAnalityczny.AddRow((Row) elem);
            this.UzupelnijOpis(elem, kd, kd.KONTO_MA, "MA");
          }
          if (!string.IsNullOrEmpty(kd.KONTO_WN))
          {
            ElemOpisuAnalitycznego elem = zrodloOpis != null ? (ElemOpisuAnalitycznego) new ElementOpisuZrodla(zrodloOpis) : (ElemOpisuAnalitycznego) new ElementOpisuEwidencji(dokEwidencji);
            KsiegaModule.GetInstance((ISessionable) this.Root.EnovaSession).OpisAnalityczny.AddRow((Row) elem);
            this.UzupelnijOpis(elem, kd, kd.KONTO_WN, "WN");
          }
        }
      }
    }

    private void UzupelnijOpis(
      ElemOpisuAnalitycznego elem,
      OptimaDocument.KwotaDodatkowa kd,
      string symbolKonta,
      string wnOrMa)
    {
      elem.InitKwotaIlosc = false;
      elem.Symbol = symbolKonta;
      elem.Kwota = (Currency) kd.KWOTA_KD_SYS;
      elem.KwotaDodatkowa = (Currency) kd.KWOTA_KD;
      elem.Opis = Soneta.Core.Tools.Left(kd.OPIS_KD.GetSafe(), 80 /*0x50*/);
      string empty = string.Empty;
      this.AppendToOpis(ref empty, kd.KATEGORIA_KD);
      this.AppendToOpis(ref empty, wnOrMa);
      elem.Wymiar = Soneta.Core.Tools.Left(empty, 128 /*0x80*/);
    }

    private void AppendToOpis(ref string wymiarStr, string addText)
    {
      if (string.IsNullOrWhiteSpace(addText))
        return;
      if (!string.IsNullOrWhiteSpace(wymiarStr))
        wymiarStr += " ";
      wymiarStr += addText.Trim();
    }
  }

  internal sealed class KwotaDodatkowa : OptimaDocument.Item
  {
    private readonly OptimaDocument.KwotaDodatkowa.TrybPracy trybPracy;
    internal const string PrivateNameVAT = "POZYCJA_KD";
    internal const string PrivateNameKB = "POZYCJA";
    internal string KATEGORIA_KD;
    internal string KATEGORIA_ID_KD;
    internal System.Decimal KWOTA_KD;
    internal System.Decimal KWOTA_KD_SYS;
    internal string KONTO_WN;
    internal string KONTO_MA;
    internal string WALUTA_KD;
    internal string KURS_NUMER_KD;
    internal System.Decimal NOTOWANIE_WALUTY_ILE_KD;
    internal System.Decimal NOTOWANIE_WALUTY_ZA_ILE_KD;
    internal Soneta.Types.Date DATA_KURSU_KD;
    internal string OPIS_KD;
    internal string KATEGORIA_KD_2;
    internal string KATEGORIA_ID_KD_2;
    internal string OPIS_KD_2;

    internal KwotaDodatkowa(
      OptimaDocument document,
      OptimaDocument.KwotaDodatkowa.TrybPracy trybPracy)
      : base(document)
    {
      this.trybPracy = trybPracy;
    }

    internal override string Name
    {
      get
      {
        return this.trybPracy != OptimaDocument.KwotaDodatkowa.TrybPracy.RejestrVAT ? "POZYCJA" : "POZYCJA_KD";
      }
    }

    protected override bool InitializeTag(string tagName)
    {
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 7:
            if (tagName == "OPIS_KD")
            {
              this.OPIS_KD = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 8:
            switch (tagName[6])
            {
              case 'K':
                if (tagName == "KWOTA_KD")
                {
                  this.KWOTA_KD = this.Root.AdvParser.ParseToDecimal();
                  return true;
                }
                break;
              case 'M':
                if (tagName == "KONTO_MA")
                {
                  this.KONTO_MA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'W':
                if (tagName == "KONTO_WN")
                {
                  this.KONTO_WN = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 9:
            switch (tagName[0])
            {
              case 'O':
                if (tagName == "OPIS_KD_2")
                {
                  this.OPIS_KD_2 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'W':
                if (tagName == "WALUTA_KD")
                {
                  this.WALUTA_KD = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 12:
            switch (tagName[1])
            {
              case 'A':
                if (tagName == "KATEGORIA_KD")
                {
                  this.KATEGORIA_KD = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'W':
                if (tagName == "KWOTA_KD_SYS")
                {
                  this.KWOTA_KD_SYS = this.Root.AdvParser.ParseToDecimal();
                  return true;
                }
                break;
            }
            break;
          case 13:
            switch (tagName[0])
            {
              case 'D':
                if (tagName == "DATA_KURSU_KD")
                {
                  this.DATA_KURSU_KD = this.Root.AdvParser.ParseToDate();
                  return true;
                }
                break;
              case 'K':
                if (tagName == "KURS_NUMER_KD")
                {
                  this.KURS_NUMER_KD = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 14:
            if (tagName == "KATEGORIA_KD_2")
            {
              this.KATEGORIA_KD_2 = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 15:
            if (tagName == "KATEGORIA_ID_KD")
            {
              this.KATEGORIA_ID_KD = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 17:
            if (tagName == "KATEGORIA_ID_KD_2")
            {
              this.KATEGORIA_ID_KD_2 = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 23:
            if (tagName == "NOTOWANIE_WALUTY_ILE_KD")
            {
              this.NOTOWANIE_WALUTY_ILE_KD = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
          case 26:
            if (tagName == "NOTOWANIE_WALUTY_ZA_ILE_KD")
            {
              this.NOTOWANIE_WALUTY_ZA_ILE_KD = this.Root.AdvParser.ParseToDecimal();
              return true;
            }
            break;
        }
      }
      return false;
    }

    internal enum TrybPracy
    {
      RejestrVAT,
      RaportKB,
    }
  }

  internal sealed class ExportProxy : OptimaDocument.BaseProxy
  {
    private string EXPORT;
    private RodzajPodmiotu? EXPORT_ENOVA;

    internal ExportProxy(OptimaDocument document)
      : base(document)
    {
    }

    internal override bool AcceptTag(string tagName)
    {
      switch (tagName)
      {
        case "EKSPORT":
          this.EXPORT = this.Root.AdvReader.ReadElementString();
          return true;
        case "EKSPORT_ENOVA":
          this.EXPORT_ENOVA = this.Root.AdvParser.ParseToRodzajPodmiotu();
          return true;
        default:
          return false;
      }
    }

    internal string GetString() => this.EXPORT?.ToLower() ?? string.Empty;

    internal RodzajPodmiotu GetRodzajPodmiotuKontrahent() => this.GetRodzajPodmiotu(true);

    internal RodzajPodmiotu GetRodzajPodmiotuKontrahentEwidencja() => this.GetRodzajPodmiotu(false);

    private RodzajPodmiotu GetRodzajPodmiotu(bool fKontrahent)
    {
      if (string.Compare(this.EXPORT, "Tak".TranslateIgnore(), true) == 0)
        return RodzajPodmiotu.Eksportowy;
      if (string.Compare(this.EXPORT, "Nie".TranslateIgnore(), true) == 0)
        return RodzajPodmiotu.Krajowy;
      return !fKontrahent ? OptimaMapper.ImportRodzajDokumentuVat(this.EXPORT) : OptimaMapper.ImportRodzajKontrahenta(this.EXPORT);
    }
  }

  internal sealed class Banki : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "BANKI";

    internal Banki(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "BANKI";

    protected override string[] SubNames
    {
      get => new string[1]{ "BANK" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Bank(this.Root, this.WERSJA);
    }

    internal OptimaDocument.Bank this[System.Guid guid] => (OptimaDocument.Bank) base[guid];

    internal OptimaDocument.Bank ByCode(string code) => (OptimaDocument.Bank) base.ByCode(code);
  }

  internal sealed class Bank : OptimaDocument.MainItem
  {
    internal const string PrivateName = "BANK";
    internal string NUMER;
    internal string AKRONIM;
    internal string NAZWA1;
    internal string NAZWA2;
    internal string KRAJ;
    internal Wojewodztwa WOJEWODZTWO;
    internal string POWIAT;
    internal string GMINA;
    internal string KOD_POCZTOWY;
    internal string POCZTA;
    internal string MIASTO;
    internal string ULICA;
    internal string NR_DOMU;
    internal string NR_LOKALU;
    internal string TELEFON;
    internal string FAX;
    internal string URL;

    internal Bank(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
    }

    public override string ToString() => $"{"BANK"}: {this.AKRONIM}";

    internal override string Name => "BANK";

    internal override string IDENT_CODE => this.AKRONIM;

    protected override bool InitializeTag(string tagName)
    {
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 3:
            switch (tagName[0])
            {
              case 'F':
                if (tagName == "FAX")
                {
                  this.FAX = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'U':
                if (tagName == "URL")
                {
                  this.URL = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 4:
            if (tagName == "KRAJ")
            {
              this.KRAJ = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 5:
            switch (tagName[0])
            {
              case 'G':
                if (tagName == "GMINA")
                {
                  this.GMINA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'N':
                if (tagName == "NUMER")
                {
                  this.NUMER = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'U':
                if (tagName == "ULICA")
                {
                  this.ULICA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 6:
            switch (tagName[5])
            {
              case '1':
                if (tagName == "NAZWA1")
                {
                  this.NAZWA1 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case '2':
                if (tagName == "NAZWA2")
                {
                  this.NAZWA2 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'A':
                if (tagName == "POCZTA")
                {
                  this.POCZTA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'O':
                if (tagName == "MIASTO")
                {
                  this.MIASTO = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'T':
                if (tagName == "POWIAT")
                {
                  this.POWIAT = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 7:
            switch (tagName[0])
            {
              case 'A':
                if (tagName == "AKRONIM")
                {
                  this.AKRONIM = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'N':
                if (tagName == "NR_DOMU")
                {
                  this.NR_DOMU = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'T':
                if (tagName == "TELEFON")
                {
                  this.TELEFON = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 9:
            if (tagName == "NR_LOKALU")
            {
              this.NR_LOKALU = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 11:
            if (tagName == "WOJEWODZTWO")
            {
              this.WOJEWODZTWO = this.Root.AdvParser.ParseToWojewodztwa();
              return true;
            }
            break;
          case 12:
            if (tagName == "KOD_POCZTOWY")
            {
              this.KOD_POCZTOWY = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
        }
      }
      return false;
    }

    internal override Row Save()
    {
      Soneta.CRM.Banki banki = CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Banki;
      if (this.IdentifiedByGuid && banki.Contains(this.ID_ZRODLA))
        return (Row) banki[this.ID_ZRODLA];
      if (banki.WgKodu[this.AKRONIM] != null)
        return (Row) banki.WgKodu[this.AKRONIM];
      Soneta.CRM.Bank bank = new Soneta.CRM.Bank();
      banki.AddRow((Row) bank);
      bank.Guid = this.IdentifiedByGuid ? this.ID_ZRODLA : System.Guid.NewGuid();
      bank.Kod = this.AKRONIM;
      bank.Nazwa = Soneta.Core.Tools.Left($"{this.NAZWA1} {this.NAZWA2}", 100);
      bank.Kierunek = this.NUMER;
      bank.Adres.Faks = this.FAX;
      bank.Adres.Gmina = this.GMINA;
      bank.Adres.KodPocztowyS = this.KOD_POCZTOWY;
      bank.Adres.Kraj = this.KRAJ;
      bank.Adres.Miejscowosc = Soneta.Core.Tools.Left(this.MIASTO, 26);
      bank.Adres.NrDomu = this.NR_DOMU;
      bank.Adres.NrLokalu = this.NR_LOKALU;
      bank.Adres.Poczta = this.POCZTA;
      bank.Adres.Powiat = this.POWIAT;
      bank.Adres.Telefon = this.TELEFON;
      bank.Adres.Ulica = this.ULICA;
      bank.Adres.Wojewodztwo = this.WOJEWODZTWO;
      bank.Kontakt.WWW = this.URL;
      return (Row) bank;
    }
  }

  internal sealed class BankProxy : OptimaDocument.ItemProxy
  {
    internal const string PrivateName = "BANK";

    internal BankProxy(OptimaDocument document)
      : base("BANK_NR", "BANK_ID", document)
    {
    }

    internal Soneta.CRM.Bank Find()
    {
      if (this.ID == System.Guid.Empty)
        return (Soneta.CRM.Bank) null;
      OptimaDocument.Bank bank = this.Root.CtnBanki != null ? this.Root.CtnBanki[this.ID] : (OptimaDocument.Bank) null;
      return bank != null ? (Soneta.CRM.Bank) bank.Save() : this.FindRowById();
    }

    private Soneta.CRM.Bank FindRowById()
    {
      Soneta.CRM.Banki banki = CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Banki;
      return !banki.Contains(this.ID) ? (Soneta.CRM.Bank) null : banki[this.ID];
    }
  }

  internal sealed class Urzedy : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "URZEDY";

    internal Urzedy(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "URZEDY";

    protected override string[] SubNames
    {
      get => new string[1]{ "URZAD" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Urzad(this.Root, this.WERSJA);
    }

    internal OptimaDocument.Urzad this[System.Guid guid] => (OptimaDocument.Urzad) base[guid];

    internal OptimaDocument.Urzad ByCode(string code) => (OptimaDocument.Urzad) base.ByCode(code);
  }

  internal sealed class Urzad : OptimaDocument.MainItem
  {
    internal const string PrivateName = "URZAD";
    internal string AKRONIM;
    internal string TYP_URZEDU;
    internal string NAZWA1;
    internal string NAZWA2;
    internal string KRAJ;
    internal Wojewodztwa WOJEWODZTWO;
    internal string POWIAT;
    internal string GMINA;
    internal string KOD_POCZTOWY;
    internal string POCZTA;
    internal string MIASTO;
    internal string ULICA;
    internal string NR_DOMU;
    internal string NR_LOKALU;
    internal string TELEFON;
    internal string FAX;
    internal string URL;
    internal string KOD;
    internal OptimaDocument.Rachunki RACHUNKI;

    internal Urzad(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.RACHUNKI = new OptimaDocument.Rachunki(document);
    }

    public override string ToString() => $"{"URZAD"}: {this.AKRONIM}";

    internal override string Name => "URZAD";

    internal override string IDENT_CODE => this.AKRONIM;

    protected override bool InitializeTag(string tagName)
    {
      switch (tagName)
      {
        case "RACHUNKI":
          return this.RACHUNKI.InitializeCollection();
        case null:
label_43:
          return false;
        default:
          switch (tagName.Length)
          {
            case 3:
              switch (tagName[0])
              {
                case 'F':
                  if (tagName == "FAX")
                  {
                    this.FAX = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'K':
                  if (tagName == "KOD")
                  {
                    this.KOD = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'U':
                  if (tagName == "URL")
                  {
                    this.URL = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                default:
                  goto label_43;
              }
            case 4:
              if (tagName == "KRAJ")
              {
                this.KRAJ = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_43;
            case 5:
              switch (tagName[0])
              {
                case 'G':
                  if (tagName == "GMINA")
                  {
                    this.GMINA = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'U':
                  if (tagName == "ULICA")
                  {
                    this.ULICA = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                default:
                  goto label_43;
              }
            case 6:
              switch (tagName[5])
              {
                case '1':
                  if (tagName == "NAZWA1")
                  {
                    this.NAZWA1 = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case '2':
                  if (tagName == "NAZWA2")
                  {
                    this.NAZWA2 = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'A':
                  if (tagName == "POCZTA")
                  {
                    this.POCZTA = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'O':
                  if (tagName == "MIASTO")
                  {
                    this.MIASTO = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'T':
                  if (tagName == "POWIAT")
                  {
                    this.POWIAT = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                default:
                  goto label_43;
              }
            case 7:
              switch (tagName[0])
              {
                case 'A':
                  if (tagName == "AKRONIM")
                  {
                    this.AKRONIM = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'N':
                  if (tagName == "NR_DOMU")
                  {
                    this.NR_DOMU = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                case 'T':
                  if (tagName == "TELEFON")
                  {
                    this.TELEFON = this.Root.AdvReader.ReadElementString();
                    return true;
                  }
                  goto label_43;
                default:
                  goto label_43;
              }
            case 9:
              if (tagName == "NR_LOKALU")
              {
                this.NR_LOKALU = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_43;
            case 10:
              if (tagName == "TYP_URZEDU")
              {
                this.TYP_URZEDU = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_43;
            case 11:
              if (tagName == "WOJEWODZTWO")
              {
                this.WOJEWODZTWO = this.Root.AdvParser.ParseToWojewodztwa();
                return true;
              }
              goto label_43;
            case 12:
              if (tagName == "KOD_POCZTOWY")
              {
                this.KOD_POCZTOWY = this.Root.AdvReader.ReadElementString();
                return true;
              }
              goto label_43;
            default:
              goto label_43;
          }
      }
    }

    internal override Row Save()
    {
      IPodmiot podmiot;
      switch (this.TYP_URZEDU)
      {
        case "US":
          UrzedySkarbowe urzedySkarbowe = CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedySkarbowe;
          if (urzedySkarbowe.Contains(this.ID_ZRODLA))
            return (Row) urzedySkarbowe[this.ID_ZRODLA];
          UrzadSkarbowy urzadSkarbowy = new UrzadSkarbowy();
          urzedySkarbowe.AddRow((Row) urzadSkarbowy);
          podmiot = (IPodmiot) urzadSkarbowy;
          urzadSkarbowy.Guid = this.ID_ZRODLA;
          urzadSkarbowy.Kod = this.AKRONIM;
          urzadSkarbowy.Nazwa = Soneta.Core.Tools.Left($"{this.NAZWA1} {this.NAZWA2}", 100);
          urzadSkarbowy.Kontakt.WWW = this.URL;
          urzadSkarbowy.KodUrzeduSkarbowego = this.KOD;
          IEnumerator enumerator1 = this.RACHUNKI.GetEnumerator();
          try
          {
            while (enumerator1.MoveNext())
            {
              OptimaDocument.Rachunek current = (OptimaDocument.Rachunek) enumerator1.Current;
              switch (current.TYP)
              {
                case "PIT5":
                  urzadSkarbowy.PIT.Parse(current.NR_RACHUNKU);
                  continue;
                case "CIT2":
                  urzadSkarbowy.CIT.Parse(current.NR_RACHUNKU);
                  continue;
                case "VAT7":
                  urzadSkarbowy.VAT.Parse(current.NR_RACHUNKU);
                  continue;
                case "PIT4":
                  urzadSkarbowy.ZalPIT.Parse(current.NR_RACHUNKU);
                  continue;
                case "AKC2":
                  urzadSkarbowy.AKC.Parse(current.NR_RACHUNKU);
                  continue;
                default:
                  continue;
              }
            }
            break;
          }
          finally
          {
            if (enumerator1 is IDisposable disposable)
              disposable.Dispose();
          }
        case "UC":
          UrzedyCelne urzedyCelne = CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedyCelne;
          if (urzedyCelne.Contains(this.ID_ZRODLA))
            return (Row) urzedyCelne[this.ID_ZRODLA];
          UrzadCelny urzadCelny = new UrzadCelny();
          urzedyCelne.AddRow((Row) urzadCelny);
          podmiot = (IPodmiot) urzadCelny;
          urzadCelny.Guid = this.ID_ZRODLA;
          urzadCelny.Kod = this.AKRONIM;
          urzadCelny.Nazwa = Soneta.Core.Tools.Left($"{this.NAZWA1} {this.NAZWA2}", 100);
          urzadCelny.Kontakt.WWW = this.URL;
          IEnumerator enumerator2 = this.RACHUNKI.GetEnumerator();
          try
          {
            while (enumerator2.MoveNext())
            {
              OptimaDocument.Rachunek current = (OptimaDocument.Rachunek) enumerator2.Current;
              if (current.TYP == "AKC2")
                urzadCelny.AKC.Parse(current.NR_RACHUNKU);
            }
            break;
          }
          finally
          {
            if (enumerator2 is IDisposable disposable)
              disposable.Dispose();
          }
        case "ZUS":
          return (Row) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY.ZUSCentrala;
        default:
          throw new Exception("Nieznany typ urzędu '{0}'".TranslateFormat((object) this.TYP_URZEDU));
      }
      podmiot.Adres.Faks = this.FAX;
      podmiot.Adres.Gmina = this.GMINA;
      podmiot.Adres.KodPocztowyS = this.KOD_POCZTOWY;
      podmiot.Adres.Kraj = this.KRAJ;
      podmiot.Adres.Miejscowosc = Soneta.Core.Tools.Left(this.MIASTO, 26);
      podmiot.Adres.NrDomu = this.NR_DOMU;
      podmiot.Adres.NrLokalu = this.NR_LOKALU;
      podmiot.Adres.Poczta = this.POCZTA;
      podmiot.Adres.Powiat = this.POWIAT;
      podmiot.Adres.Telefon = this.TELEFON;
      podmiot.Adres.Ulica = this.ULICA;
      podmiot.Adres.Wojewodztwo = this.WOJEWODZTWO;
      return (Row) podmiot;
    }
  }

  internal sealed class UrzadProxy : OptimaDocument.ItemProxy
  {
    internal const string PrivateName = "URZAD_SKARBOWY";

    internal UrzadProxy(OptimaDocument document)
      : base(document, "URZAD_SKARBOWY")
    {
    }

    internal IPodmiot Find()
    {
      if (this.ID == System.Guid.Empty)
        return (IPodmiot) null;
      OptimaDocument.Urzad urzad = this.Root.CtnUrzedy != null ? this.Root.CtnUrzedy[this.ID] : (OptimaDocument.Urzad) null;
      return urzad != null ? (IPodmiot) urzad.Save() : (IPodmiot) this.FindRowById();
    }

    private GuidedRow FindRowById()
    {
      GuidedTable zusy = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).ZUSY;
      if (zusy.Contains(this.ID))
        return zusy[this.ID];
      GuidedTable urzedySkarbowe = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedySkarbowe;
      if (urzedySkarbowe.Contains(this.ID))
        return urzedySkarbowe[this.ID];
      GuidedTable urzedyCelne = (GuidedTable) CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).UrzedyCelne;
      return urzedyCelne.Contains(this.ID) ? urzedyCelne[this.ID] : (GuidedRow) null;
    }
  }

  internal sealed class Pracownicy : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "PRACOWNICY";

    internal Pracownicy(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "PRACOWNICY";

    protected override string[] SubNames
    {
      get => new string[1]{ "PRACOWNIK" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Pracownik(this.Root, this.WERSJA);
    }

    internal OptimaDocument.Pracownik this[System.Guid guid]
    {
      get => (OptimaDocument.Pracownik) base[guid];
    }

    internal OptimaDocument.Pracownik ByCode(string code)
    {
      return (OptimaDocument.Pracownik) base.ByCode(code);
    }
  }

  internal sealed class Wspolnicy : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "WSPOLNICY";

    internal Wspolnicy(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "WSPOLNICY";

    protected override string[] SubNames
    {
      get => new string[1]{ "WSPOLNIK" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Wspolnik(this.Root, this.WERSJA);
    }

    internal OptimaDocument.Wspolnik this[System.Guid guid] => (OptimaDocument.Wspolnik) base[guid];

    internal OptimaDocument.Wspolnik ByCode(string code)
    {
      return (OptimaDocument.Wspolnik) base.ByCode(code);
    }
  }

  internal abstract class PracownikOrWspolnik : OptimaDocument.MainItem
  {
    internal string AKRONIM;
    internal string NAZWISKO;
    internal string IMIE1;
    internal string IMIE2;
    internal string IMIE_OJCA;
    internal string IMIE_MATKI;
    internal Soneta.Types.Date DATA_URODZENIA;
    internal string MIEJSCE_URODZENIA;
    internal string NAZWISKO_RODOWE;
    internal string NAZWISKO_RODOWE_MATKI;
    internal string PESEL;
    internal string NIP;
    internal string TELEFON1;
    internal string NR_RACHUNKU;
    internal OptimaDocument.Adresy ADRESY;
    internal OptimaDocument.UrzadProxy URZAD_SKARBOWY;
    internal OptimaDocument.BankProxy BANK;

    protected PracownikOrWspolnik(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.ADRESY = new OptimaDocument.Adresy(document);
      this.URZAD_SKARBOWY = new OptimaDocument.UrzadProxy(document);
      this.BANK = new OptimaDocument.BankProxy(document);
    }

    internal override string IDENT_CODE => this.AKRONIM;

    protected override bool InitializeTag(string tagName)
    {
      if (tagName == "ADRESY")
        return this.ADRESY.InitializeCollection();
      if (this.BANK.AcceptTag(tagName) || this.URZAD_SKARBOWY.AcceptTag(tagName))
        return true;
      if (tagName != null)
      {
        switch (tagName.Length)
        {
          case 3:
            if (tagName == "NIP")
            {
              this.NIP = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 5:
            switch (tagName[4])
            {
              case '1':
                if (tagName == "IMIE1")
                {
                  this.IMIE1 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case '2':
                if (tagName == "IMIE2")
                {
                  this.IMIE2 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'L':
                if (tagName == "PESEL")
                {
                  this.PESEL = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 7:
            if (tagName == "AKRONIM")
            {
              this.AKRONIM = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 8:
            switch (tagName[0])
            {
              case 'N':
                if (tagName == "NAZWISKO")
                {
                  this.NAZWISKO = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 'T':
                if (tagName == "TELEFON1")
                {
                  this.TELEFON1 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
            break;
          case 9:
            if (tagName == "IMIE_OJCA")
            {
              this.IMIE_OJCA = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 10:
            if (tagName == "IMIE_MATKI")
            {
              this.IMIE_MATKI = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 11:
            if (tagName == "NR_RACHUNKU")
            {
              this.NR_RACHUNKU = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 14:
            if (tagName == "DATA_URODZENIA")
            {
              this.DATA_URODZENIA = this.Root.AdvParser.ParseToDate();
              return true;
            }
            break;
          case 15:
            if (tagName == "NAZWISKO_RODOWE")
            {
              this.NAZWISKO_RODOWE = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 17:
            if (tagName == "MIEJSCE_URODZENIA")
            {
              this.MIEJSCE_URODZENIA = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
          case 21:
            if (tagName == "NAZWISKO_RODOWE_MATKI")
            {
              this.NAZWISKO_RODOWE_MATKI = this.Root.AdvReader.ReadElementString();
              return true;
            }
            break;
        }
      }
      return false;
    }

    internal Row Save(bool fPracownik)
    {
      Soneta.Kadry.Pracownicy pracownicy = KadryModule.GetInstance((ISessionable) this.Root.EnovaSession).Pracownicy;
      if (pracownicy.Contains(this.ID_ZRODLA))
        return (Row) pracownicy[this.ID_ZRODLA];
      if (fPracownik && pracownicy.WgKodu[this.AKRONIM] != null)
        return (Row) pracownicy.WgKodu[this.AKRONIM];
      Soneta.Kadry.Pracownik pracownik = fPracownik ? (Soneta.Kadry.Pracownik) new PracownikFirmy() : (Soneta.Kadry.Pracownik) new Wlasciciel();
      pracownicy.AddRow((Row) pracownik);
      pracownik.Guid = this.ID_ZRODLA;
      pracownik.Kod = this.AKRONIM;
      PracHistoria last = pracownik.Last;
      last.Imie = this.IMIE1;
      last.ImieDrugie = this.IMIE2;
      last.Nazwisko = this.NAZWISKO;
      last.ImieOjca = this.IMIE_OJCA;
      last.ImieMatki = this.IMIE_MATKI;
      last.Urodzony.Data = this.DATA_URODZENIA;
      last.Urodzony.Miejsce = this.MIEJSCE_URODZENIA;
      last.NazwiskoRodowe = this.NAZWISKO_RODOWE;
      last.NazwiskoRodoweMatki = this.NAZWISKO_RODOWE_MATKI;
      last.PESEL = this.PESEL;
      last.NIP = this.NIP;
      foreach (OptimaDocument.Adres adres in (ReadOnlyCollectionBase) this.ADRESY)
      {
        if (adres.STATUS.ToLower() == "zameldowania".TranslateIgnore())
        {
          Soneta.Core.Adres adresZameldowania = last.AdresZameldowania;
          adresZameldowania.Kraj = adres.KRAJ;
          adresZameldowania.Wojewodztwo = adres.WOJEWODZTWO;
          adresZameldowania.Powiat = adres.POWIAT;
          adresZameldowania.Gmina = adres.GMINA;
          adresZameldowania.Ulica = adres.ULICA;
          adresZameldowania.NrDomu = adres.NR_DOMU;
          adresZameldowania.NrLokalu = adres.NR_LOKALU;
          adresZameldowania.Miejscowosc = adres.MIASTO;
          adresZameldowania.KodPocztowyS = adres.KOD_POCZTOWY;
          adresZameldowania.Poczta = adres.POCZTA;
          break;
        }
      }
      last.AdresZameldowania.Telefon = this.TELEFON1;
      last.Podatki.UrzadSkarbowy = (IPodmiotUI) (this.URZAD_SKARBOWY.Find() as UrzadSkarbowy);
      if (!string.IsNullOrEmpty(this.NR_RACHUNKU))
      {
        RachunekBankowyPracownika bankowyPracownika = (RachunekBankowyPracownika) pracownik.Rachunki.GetNext(Array.Empty<object>());
        if (bankowyPracownika == null)
        {
          bankowyPracownika = new RachunekBankowyPracownika(pracownik);
          KasaModule.GetInstance((ISessionable) pracownik).RachBankPodmiot.AddRow((Row) bankowyPracownika);
        }
        bankowyPracownika.Rachunek.Bank = (IBank) this.BANK.Find();
        bankowyPracownika.Rachunek.Numer.Parse(this.NR_RACHUNKU);
      }
      return (Row) pracownik;
    }
  }

  internal sealed class Pracownik : OptimaDocument.PracownikOrWspolnik
  {
    internal const string PrivateName = "PRACOWNIK";

    internal Pracownik(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
    }

    public override string ToString() => $"{"PRACOWNIK"}: {this.AKRONIM}";

    internal override string Name => "PRACOWNIK";

    internal override Row Save() => this.Save(true);
  }

  internal sealed class Wspolnik : OptimaDocument.PracownikOrWspolnik
  {
    internal const string PrivateName = "WSPOLNIK";

    internal Wspolnik(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
    }

    public override string ToString() => $"{"WSPOLNIK"}: {this.AKRONIM}";

    internal override string Name => "WSPOLNIK";

    internal override Row Save() => this.Save(false);
  }

  internal sealed class Kontrahenci : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "KONTRAHENCI";

    internal Kontrahenci(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "KONTRAHENCI";

    protected override string[] SubNames
    {
      get => new string[1]{ "KONTRAHENT" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.Kontrahent(this.Root, this.WERSJA);
    }

    internal OptimaDocument.Kontrahent this[System.Guid guid]
    {
      get => (OptimaDocument.Kontrahent) base[guid];
    }

    internal OptimaDocument.Kontrahent ByCode(string code)
    {
      return (OptimaDocument.Kontrahent) base.ByCode(code);
    }
  }

  internal sealed class Kontrahent : OptimaDocument.MainItem
  {
    internal const string PrivateName = "KONTRAHENT";
    internal string AKRONIM;
    internal bool INDYWIDUALNY_TERMIN;
    internal bool FINALNY;
    internal bool PLATNIK_VAT = true;
    internal int TERMIN;
    internal OptimaDocument.Adresy ADRESY;
    internal OptimaDocument.ExportProxy EXPORT;

    internal Kontrahent(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.ADRESY = new OptimaDocument.Adresy(document);
      this.EXPORT = new OptimaDocument.ExportProxy(document);
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
    }

    public override string ToString() => $"{"KONTRAHENT"}: {this.Akronim}";

    internal override string Name => "KONTRAHENT";

    internal override string IDENT_CODE => this.Akronim;

    private string Akronim
    {
      get
      {
        if (!string.IsNullOrEmpty(this.AKRONIM))
          return this.AKRONIM;
        return this.Adres == null ? string.Empty : this.Adres.AKRONIM;
      }
    }

    private OptimaDocument.Adres Adres
    {
      get
      {
        foreach (object adres in (ReadOnlyCollectionBase) this.ADRESY)
        {
          if (!(adres is OptimaDocument.AdresKorespondencyjny))
            return adres as OptimaDocument.Adres;
        }
        return (OptimaDocument.Adres) null;
      }
    }

    protected override bool InitializeTag(string tagName)
    {
      if (tagName == "ADRESY")
        return this.ADRESY.InitializeCollection();
      if (this.EXPORT.AcceptTag(tagName))
        return true;
      switch (tagName)
      {
        case "AKRONIM":
          this.AKRONIM = this.Root.AdvReader.ReadElementString();
          return true;
        case "INDYWIDUALNY_TERMIN":
          this.INDYWIDUALNY_TERMIN = this.Root.AdvParser.ParseToBool();
          return true;
        case "TERMIN":
          this.TERMIN = this.Root.AdvParser.ParseToInt();
          return true;
        case "FINALNY":
          this.FINALNY = this.Root.AdvParser.ParseToBool();
          return true;
        case "PLATNIK_VAT":
          string str = this.Root.AdvReader.ReadElementString();
          this.PLATNIK_VAT = string.IsNullOrWhiteSpace(str) || this.Root.AdvParser.ParseToBool(str);
          return true;
        default:
          return false;
      }
    }

    internal override Row Save()
    {
      Soneta.CRM.Kontrahenci kontrahenci = CRMModule.GetInstance((ISessionable) this.Root.EnovaSession).Kontrahenci;
      if (this.IdentifiedByGuid && kontrahenci.Contains(this.ID_ZRODLA))
        return (Row) kontrahenci[this.ID_ZRODLA];
      OptimaDocument.Adres adres = this.Adres;
      string euvat = (adres.NIP_KRAJ + adres.NIP).Replace(" ", "").Replace("-", "");
      if (!string.IsNullOrEmpty(euvat))
      {
        Soneta.CRM.Kontrahent next = kontrahenci.WgEuVAT[euvat].GetNext(Array.Empty<object>());
        if (next != null)
          return (Row) next;
      }
      if (kontrahenci.WgKodu[this.Akronim] != null)
        return (Row) kontrahenci.WgKodu[this.Akronim];
      Soneta.CRM.Kontrahent kontrahent = new Soneta.CRM.Kontrahent();
      kontrahenci.AddRow((Row) kontrahent);
      kontrahent.Guid = this.IdentifiedByGuid ? this.ID_ZRODLA : System.Guid.NewGuid();
      kontrahent.Kod = this.Akronim;
      kontrahent.StatusPodmiotu = this.FINALNY ? StatusPodmiotu.Finalny : StatusPodmiotu.PodmiotGospodarczy;
      kontrahent.PodatnikVAT = this.PLATNIK_VAT;
      kontrahent.RodzajPodmiotu = this.EXPORT.GetRodzajPodmiotuKontrahent();
      kontrahent.EuVAT = adres.NIP_KRAJ + adres.NIP;
      kontrahent.Nazwa = $"{adres.NAZWA1} {adres.NAZWA2} {adres.NAZWA3}".Trim();
      if (this.INDYWIDUALNY_TERMIN)
        kontrahent.Termin = this.TERMIN;
      adres.CopyTo(kontrahent.Adres);
      adres.CopyTo(kontrahent.Kontakt);
      return (Row) kontrahent;
    }
  }

  internal sealed class RaportyKB : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "RAPORTY_KB";

    internal RaportyKB(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "RAPORTY_KB";

    protected override string[] SubNames
    {
      get => new string[1]{ "RAPORT_KB" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.RaportKB(this.Root, this.WERSJA);
    }

    public OptimaDocument.RaportKB this[System.Guid guid] => (OptimaDocument.RaportKB) base[guid];
  }

  internal sealed class RaportKB : OptimaDocument.MainItem
  {
    internal const string PrivateName = "RAPORT_KB";
    internal Soneta.Types.Date DATA_ZAMKNIECIA;
    internal string NUMER;
    internal OptimaDocument.RachunekProxy RACHUNEK;
    internal OptimaDocument.ZapisyKB ZAPISY_KB;

    internal RaportKB(OptimaDocument document, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.RACHUNEK = new OptimaDocument.RachunekProxy(document);
      this.ZAPISY_KB = new OptimaDocument.ZapisyKB(document, fmtVersion);
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
    }

    public override string ToString() => $"{this.Name}: {this.NUMER} ({this.DATA_ZAMKNIECIA})";

    internal override string Name => "RAPORT_KB";

    protected override bool InitializeTag(string tagName)
    {
      if (tagName == "ZAPISY_KB")
        return this.ZAPISY_KB.InitializeCollection();
      if (this.RACHUNEK.AcceptTag(tagName))
        return true;
      switch (tagName)
      {
        case "DATA_ZAMKNIECIA":
          this.DATA_ZAMKNIECIA = this.Root.AdvParser.ParseToDate();
          return true;
        case "NUMER":
          this.NUMER = this.Root.AdvReader.ReadElementString();
          return true;
        default:
          return false;
      }
    }

    internal Row Save(OptimaImportStats stats)
    {
      KasaModule instance = KasaModule.GetInstance((ISessionable) this.Root.EnovaSession);
      RaportESPEwidencja next = (RaportESPEwidencja) CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).DokEwidencja.WgNumeruDokumentu[this.NUMER, this.DATA_ZAMKNIECIA].GetNext(Array.Empty<object>());
      if (next != null)
      {
        this.Root.importLog.WriteLine("Informacja - dokument {0} z dnia {1} już istnieje w systemie - zostanie pominięty".TranslateFormat((object) this.NUMER, (object) this.DATA_ZAMKNIECIA));
        ++stats.Duplicates;
        return (Row) next;
      }
      EwidencjaSP ewidencjaSP;
      if (this.Root.ImportFilter.Params.EwidencjaSP != null)
        ewidencjaSP = this.Root.ImportFilter.Params.EwidencjaSP;
      else if ((ewidencjaSP = instance.EwidencjeSP.WgSymbolu[this.RACHUNEK.Kod].GetNext(Array.Empty<object>())) == null)
        throw new Exception("Nie znaleziono ewidencji ŚP o symbolu {0}".TranslateFormat((object) this.RACHUNEK.Kod));
      RaportESPEwidencja ewidencja = ewidencjaSP.Typ == TypEwidencjiSP.Kasa ? (RaportESPEwidencja) new RaportKasowyEwidencja(ewidencjaSP) : (RaportESPEwidencja) new WyciagBankowyEwidencja(ewidencjaSP);
      CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).DokEwidencja.AddRow((Row) ewidencja);
      if (ewidencja.EwidencjaSP.DefinicjaED != null)
        ewidencja.Definicja = ewidencja.EwidencjaSP.DefinicjaED;
      ewidencja.Numer.Numer = this.Root.GetOstatniNumer(ewidencja.Numer.Symbol) + 1;
      ewidencja.NumerDokumentu = this.NUMER;
      ewidencja.DataWplywu = this.DATA_ZAMKNIECIA;
      ewidencja.DataDokumentu = this.DATA_ZAMKNIECIA;
      ewidencja.Opis = this.Root.ImportFilter.Params.DomyslnyOpis;
      foreach (OptimaDocument.ZapisKB zapisKb in (ReadOnlyCollectionBase) this.ZAPISY_KB)
        zapisKb.Save(ewidencja);
      ++stats.Documents;
      return (Row) ewidencja;
    }
  }

  internal sealed class RejestrySprzedazyVAT : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "REJESTRY_SPRZEDAZY_VAT";

    internal RejestrySprzedazyVAT(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "REJESTRY_SPRZEDAZY_VAT";

    protected override string[] SubNames
    {
      get => new string[1]{ "REJESTR_SPRZEDAZY_VAT" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.RejestrSprzedazyVAT(this.Root, this.WERSJA);
    }

    public OptimaDocument.RejestrSprzedazyVAT this[System.Guid guid]
    {
      get => (OptimaDocument.RejestrSprzedazyVAT) base[guid];
    }
  }

  internal sealed class RejestryZakupuVAT : OptimaDocument.MainItemsCollection
  {
    internal const string PrivateName = "REJESTRY_ZAKUPU_VAT";

    internal RejestryZakupuVAT(OptimaDocument document)
      : base(document)
    {
    }

    protected override string Name => "REJESTRY_ZAKUPU_VAT";

    protected override string[] SubNames
    {
      get => new string[1]{ "REJESTR_ZAKUPU_VAT" };
    }

    protected override OptimaDocument.Item CreateItem(string name)
    {
      return (OptimaDocument.Item) new OptimaDocument.RejestrZakupuVAT(this.Root, this.WERSJA);
    }

    public OptimaDocument.RejestrZakupuVAT this[System.Guid guid]
    {
      get => (OptimaDocument.RejestrZakupuVAT) base[guid];
    }
  }

  internal sealed class RejestrSprzedazyVAT : OptimaDocument.RejestrVAT
  {
    internal const string PrivateName = "REJESTR_SPRZEDAZY_VAT";

    internal RejestrSprzedazyVAT(OptimaDocument document, float fmtVersion)
      : base(document, TypDokumentu.SprzedażEwidencja, fmtVersion)
    {
    }

    internal override string Name => "REJESTR_SPRZEDAZY_VAT";
  }

  internal sealed class RejestrZakupuVAT : OptimaDocument.RejestrVAT
  {
    internal const string PrivateName = "REJESTR_ZAKUPU_VAT";

    internal RejestrZakupuVAT(OptimaDocument document, float fmtVersion)
      : base(document, TypDokumentu.ZakupEwidencja, fmtVersion)
    {
    }

    internal override string Name => "REJESTR_ZAKUPU_VAT";
  }

  internal abstract class RejestrVAT : OptimaDocument.MainItem
  {
    private readonly TypDokumentu Typ;
    internal string REJESTR;
    internal Soneta.Types.Date DATA_WYSTAWIENIA;
    internal Soneta.Types.Date DATA_SPRZEDAZY;
    internal Soneta.Types.Date DATA_ZAKUPU;
    internal Soneta.Types.Date DATA_WPLYWU;
    internal string NUMER;
    internal bool KOREKTA;
    internal string KOREKTA_NUMER;
    internal bool FISKALNA;
    internal bool FINALNY;
    internal string OPIS;
    internal string DEKLARACJA_VAT7;
    internal OptimaDocument.PodmiotProxy PODMIOT;
    internal OptimaDocument.ExportProxy EXPORT;
    internal OptimaDocument.Platnosci PLATNOSCI;
    internal OptimaDocument.PozycjeRejestruVAT POZYCJE;
    internal OptimaDocument.KwotyDodatkowe KWOTY_DODATKOWE;
    internal bool MPP;
    internal string KRAJ_WYDANIA_W;
    internal string KOD_KRAJU_ODBIORCY;
    internal string WALUTA;
    internal OptimaDocument.RejestrVAT.ProceduryVAT PROCEDURY_VAT;

    internal RejestrVAT(OptimaDocument document, TypDokumentu typ, float fmtVersion)
      : base(document, fmtVersion)
    {
      this.Typ = typ;
      this.IdentifiedByGuid = !document.ImportFilter.Params.IgnoreGuids;
      this.PODMIOT = new OptimaDocument.PodmiotProxy(document);
      this.EXPORT = new OptimaDocument.ExportProxy(document);
      this.POZYCJE = new OptimaDocument.PozycjeRejestruVAT(document);
      this.PLATNOSCI = new OptimaDocument.Platnosci(document, fmtVersion);
      this.KWOTY_DODATKOWE = new OptimaDocument.KwotyDodatkowe(document, OptimaDocument.KwotaDodatkowa.TrybPracy.RejestrVAT);
      this.PROCEDURY_VAT = new OptimaDocument.RejestrVAT.ProceduryVAT(document);
    }

    public override string ToString() => $"{this.Name}: {this.NUMER} ({this.DATA_WYSTAWIENIA})";

    protected override bool InitializeTag(string tagName)
    {
      switch (tagName)
      {
        case "POZYCJE":
          return this.POZYCJE.InitializeCollection();
        case "PLATNOSCI":
          return this.PLATNOSCI.InitializeCollection();
        case "KWOTY_DODATKOWE":
          return this.KWOTY_DODATKOWE.InitializeCollection();
        case "KODY_JPK":
          return this.PROCEDURY_VAT.InitializeCollection();
        default:
          if (this.PODMIOT.AcceptTag(tagName) || this.EXPORT.AcceptTag(tagName))
            return true;
          if (tagName != null)
          {
            switch (tagName.Length)
            {
              case 3:
                if (tagName == "MPP")
                {
                  this.MPP = this.Root.AdvParser.ParseToBool();
                  return true;
                }
                break;
              case 4:
                if (tagName == "OPIS")
                {
                  this.OPIS = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 5:
                if (tagName == "NUMER")
                {
                  this.NUMER = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 6:
                if (tagName == "WALUTA")
                {
                  this.WALUTA = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 7:
                switch (tagName[0])
                {
                  case 'F':
                    if (tagName == "FINALNY")
                    {
                      this.FINALNY = this.Root.AdvParser.ParseToBool();
                      return true;
                    }
                    break;
                  case 'K':
                    if (tagName == "KOREKTA")
                    {
                      this.KOREKTA = this.Root.AdvParser.ParseToBool();
                      return true;
                    }
                    break;
                  case 'R':
                    if (tagName == "REJESTR")
                    {
                      this.REJESTR = this.Root.AdvReader.ReadElementString();
                      return true;
                    }
                    break;
                }
                break;
              case 8:
                if (tagName == "FISKALNA")
                {
                  this.FISKALNA = this.Root.AdvParser.ParseToBool();
                  return true;
                }
                break;
              case 11:
                switch (tagName[5])
                {
                  case 'W':
                    if (tagName == "DATA_WPLYWU")
                    {
                      this.DATA_WPLYWU = this.Root.AdvParser.ParseToDate();
                      return true;
                    }
                    break;
                  case 'Z':
                    if (tagName == "DATA_ZAKUPU")
                    {
                      this.DATA_ZAKUPU = this.Root.AdvParser.ParseToDate();
                      return true;
                    }
                    break;
                }
                break;
              case 13:
                if (tagName == "KOREKTA_NUMER")
                {
                  this.KOREKTA_NUMER = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 14:
                switch (tagName[0])
                {
                  case 'D':
                    if (tagName == "DATA_SPRZEDAZY")
                    {
                      this.DATA_SPRZEDAZY = this.Root.AdvParser.ParseToDate();
                      return true;
                    }
                    break;
                  case 'K':
                    if (tagName == "KRAJ_WYDANIA_W")
                    {
                      this.KRAJ_WYDANIA_W = this.Root.AdvReader.ReadElementString();
                      return true;
                    }
                    break;
                }
                break;
              case 15:
                if (tagName == "DEKLARACJA_VAT7")
                {
                  this.DEKLARACJA_VAT7 = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
              case 16 /*0x10*/:
                if (tagName == "DATA_WYSTAWIENIA")
                {
                  this.DATA_WYSTAWIENIA = this.Root.AdvParser.ParseToDate();
                  return true;
                }
                break;
              case 18:
                if (tagName == "KOD_KRAJU_ODBIORCY")
                {
                  this.KOD_KRAJU_ODBIORCY = this.Root.AdvReader.ReadElementString();
                  return true;
                }
                break;
            }
          }
          return false;
      }
    }

    [TranslateIgnore]
    internal Row Save(OptimaImportStats stats)
    {
      bool flag = EwidencjaVatModule.GetInstance((ISessionable) this.Root.EnovaSession).Config.Ogólne.FirmaPodlegaVAT;
      if (this.POZYCJE.Count == 0)
        flag = false;
      DokEwidencja dokEwidencja = CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).DokEwidencja;
      SubTable subTable = (SubTable) dokEwidencja.WgNumeruDokumentu[this.NUMER, this.DATA_WYSTAWIENIA];
      IPodmiot podmiot = this.PODMIOT.Find();
      if (!subTable.IsEmpty)
      {
        foreach (DokEwidencji dokEwidencji in subTable)
        {
          if (dokEwidencji.Typ == this.Typ && dokEwidencji.Podmiot == podmiot)
          {
            this.Root.importLog.WriteLine("Informacja - dokument {0} z dnia {1} już istnieje w systemie - zostanie pominięty".TranslateFormat((object) this.NUMER, (object) this.DATA_WYSTAWIENIA));
            ++stats.Duplicates;
            return (Row) dokEwidencji;
          }
        }
      }
      HandlowyEwidencja handlowyEwidencja = this.Typ == TypDokumentu.ZakupEwidencja ? (HandlowyEwidencja) new ZakupEwidencja() : (HandlowyEwidencja) new SprzedazEwidencja();
      dokEwidencja.AddRow((Row) handlowyEwidencja);
      handlowyEwidencja.AutoPlatnosci = false;
      DefinicjaDokumentu definicjaDokumentu = this.Root.ImportFilter.Params.Definicja != null ? this.Root.ImportFilter.Params.Definicja : CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).DefDokumentow.WgTypu[this.Typ, this.REJESTR];
      if (definicjaDokumentu != null)
        handlowyEwidencja.Definicja = definicjaDokumentu;
      handlowyEwidencja.DataWplywu = this.Typ == TypDokumentu.ZakupEwidencja ? this.DATA_WPLYWU : this.DATA_WYSTAWIENIA;
      handlowyEwidencja.NumerDokumentu = this.NUMER;
      handlowyEwidencja.DataDokumentu = this.DATA_WYSTAWIENIA;
      handlowyEwidencja.DataOperacji = this.Typ == TypDokumentu.ZakupEwidencja ? this.DATA_ZAKUPU : this.DATA_SPRZEDAZY;
      handlowyEwidencja.Podmiot = podmiot;
      handlowyEwidencja.Opis = string.IsNullOrEmpty(this.OPIS) ? this.Root.ImportFilter.Params.DomyslnyOpis : Soneta.Core.Tools.Left(this.OPIS, 80 /*0x50*/);
      if (flag)
      {
        if (handlowyEwidencja.NagEwidencjiVAT == null)
          throw new ApplicationException("Brak nagłówka VAT, dokumenty muszą domyślnie podlegać VAT".Translate());
        if (this.KOREKTA)
          handlowyEwidencja.NagEwidencjiVAT.NrDokumentuK = this.KOREKTA_NUMER;
        handlowyEwidencja.NagEwidencjiVAT.StatusPodmiotu = this.FINALNY ? StatusPodmiotu.Finalny : StatusPodmiotu.PodmiotGospodarczy;
        handlowyEwidencja.NagEwidencjiVAT.RodzajPodmiotu = this.EXPORT.GetRodzajPodmiotuKontrahentEwidencja();
        if (this.Typ == TypDokumentu.SprzedażEwidencja && !string.IsNullOrEmpty(this.KRAJ_WYDANIA_W))
        {
          NagEwidencjiVAT nagEwidencjiVat = handlowyEwidencja.NagEwidencjiVAT;
          nagEwidencjiVat.KrajWydania = CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).KrajeTbl.WgKodu2[this.KRAJ_WYDANIA_W] ?? throw new Exception("Nie odnaleziono kraju o kodzie '{0}'.".TranslateFormat((object) this.KRAJ_WYDANIA_W));
        }
        if (!(this.DEKLARACJA_VAT7 == "") && this.DEKLARACJA_VAT7 != null && !(this.DEKLARACJA_VAT7 == "Nie".TranslateIgnore()))
        {
          handlowyEwidencja.NagEwidencjiVAT.DefinicjaPowstaniaObowiazku = EwidencjaVatModule.GetInstance((ISessionable) this.Root.EnovaSession).DefinicjePOVAT[this.Typ == TypDokumentu.ZakupEwidencja ? DefinicjaPowstaniaObowiazkuVAT.WgDowolnejDatyZakup : DefinicjaPowstaniaObowiazkuVAT.WgDowolnejDatySprzedaz];
          if (this.DEKLARACJA_VAT7.Length == 7)
            handlowyEwidencja.NagEwidencjiVAT.DataPowstania = new YearMonth(int.Parse(this.DEKLARACJA_VAT7.Substring(0, 4)), int.Parse(this.DEKLARACJA_VAT7.Substring(5, 2))).LastDay;
          else
            handlowyEwidencja.NagEwidencjiVAT.DataPowstania = new YearMonth(int.Parse(this.DEKLARACJA_VAT7.Substring(0, 4)), int.Parse(this.DEKLARACJA_VAT7.Substring(4, 2))).LastDay;
        }
        DefStawekVat defStawekVat = CoreModule.GetInstance((ISessionable) this.Root.EnovaSession).DefStawekVat;
        EleEwidencjiVATT eleEwidencjiVatt = EwidencjaVatModule.GetInstance((ISessionable) this.Root.EnovaSession).EleEwidencjiVATT;
        foreach (OptimaDocument.PozycjaRejestruVAT pozycjaRejestruVat in (ReadOnlyCollectionBase) this.POZYCJE)
        {
          ElemEwidencjiVAT session = this.Typ == TypDokumentu.ZakupEwidencja ? (ElemEwidencjiVAT) new ElemEwidencjiVATZakup((VATEwidencja) handlowyEwidencja) : (ElemEwidencjiVAT) new ElemEwidencjiVATSprzedaz((VATEwidencja) handlowyEwidencja);
          eleEwidencjiVatt.AddRow((Row) session);
          if (!string.IsNullOrEmpty(pozycjaRejestruVat.OPIS_POZ))
            session.OpisDodatkowy = pozycjaRejestruVat.OPIS_POZ;
          StatusStawkiVat status;
          switch (string.IsNullOrEmpty(pozycjaRejestruVat.STATUS_VAT) ? pozycjaRejestruVat.STATUS_VAT : pozycjaRejestruVat.STATUS_VAT.ToLower())
          {
            case "opodatkowana":
              status = StatusStawkiVat.Opodatkowana;
              break;
            case "zwolniona":
              status = StatusStawkiVat.Zwolniona;
              break;
            case "zaniżona":
              status = StatusStawkiVat.Zaniżona;
              break;
            case "nie podlega":
              status = StatusStawkiVat.NiePodlega;
              break;
            case "brak":
              status = StatusStawkiVat.Brak;
              break;
            default:
              status = StatusStawkiVat.Opodatkowana;
              break;
          }
          KrajTbl poland;
          if (!string.IsNullOrEmpty(this.KOD_KRAJU_ODBIORCY))
          {
            poland = dokEwidencja.Module.KrajeTbl.WgKodu2[this.KOD_KRAJU_ODBIORCY];
            if (poland == null)
              throw new Exception("Nie zdefiniowano kraju o kodzie {0}.".TranslateFormat((object) this.KOD_KRAJU_ODBIORCY));
          }
          else
            poland = dokEwidencja.Module.KrajeTbl.Poland;
          Soneta.Types.Percent procent = new Soneta.Types.Percent(pozycjaRejestruVat.STAWKA_VAT / 100M);
          session.DefinicjaStawki = defStawekVat[status, procent, poland, false] ?? throw new Exception("Nie zdefiniowano stawki VAT {0} dla kraju {1}.".TranslateFormat((object) procent, (object) poland.Nazwa));
          if (string.IsNullOrEmpty(this.WALUTA) || string.Equals(this.WALUTA, "PLN", StringComparison.CurrentCultureIgnoreCase) || poland.CzyPolska)
          {
            if (pozycjaRejestruVat.NETTO_SYS > 0M)
            {
              session.Netto = new Currency(pozycjaRejestruVat.NETTO_SYS);
              session.VAT = new Currency(pozycjaRejestruVat.VAT_SYS);
            }
            else
            {
              session.Netto = new Currency(pozycjaRejestruVat.NETTO);
              session.VAT = new Currency(pozycjaRejestruVat.VAT);
            }
          }
          else
          {
            session.Netto = new Currency(pozycjaRejestruVat.NETTO, this.WALUTA);
            session.VAT = new Currency(pozycjaRejestruVat.VAT, this.WALUTA);
          }
          if (this.MPP)
            session.Grupa = GrupaElementuVAT.MPP;
          if (!session.Session.Login.GetLicenceData().Purchased[LicencjaProgramu.KS] && session.Session.Login.GetLicenceData().Purchased[LicencjaProgramu.KPiR] && KsiegaModule.GetInstance((ISessionable) session).Config.KPiR.Typ == TypOkresuObrachunkowego.KPiR)
          {
            string str = string.IsNullOrEmpty(pozycjaRejestruVat.KOLUMNA_KPR) ? pozycjaRejestruVat.KOLUMNA_KPR : pozycjaRejestruVat.KOLUMNA_KPR.ToLower();
            if (str != null)
            {
              switch (str.Length)
              {
                case 0:
                  goto label_77;
                case 8:
                  if (str == "sprzedaż")
                  {
                    session.Kolumna = NrKolumnyKPiR.Sprzedaż;
                    goto label_77;
                  }
                  break;
                case 9:
                  if (str == "zaszłości")
                  {
                    session.Kolumna = NrKolumnyKPiR.Zaszłości;
                    goto label_77;
                  }
                  break;
                case 13:
                  switch (str[0])
                  {
                    case 'n':
                      if (str == "nie księgować")
                      {
                        session.Kolumna = NrKolumnyKPiR.NieDotyczy;
                        goto label_77;
                      }
                      break;
                    case 'w':
                      if (str == "wynagrodzenia")
                      {
                        session.Kolumna = NrKolumnyKPiR.Wynagrodzenia;
                        goto label_77;
                      }
                      break;
                    case 'z':
                      if (str == "zakup towarów")
                      {
                        session.Kolumna = NrKolumnyKPiR.ZakupTowarów;
                        goto label_77;
                      }
                      break;
                  }
                  break;
                case 14:
                  if (str == "koszty uboczne")
                  {
                    session.Kolumna = NrKolumnyKPiR.KosztyUboczne;
                    goto label_77;
                  }
                  break;
                case 16 /*0x10*/:
                  switch (str[0])
                  {
                    case 'b':
                      if (str == "badania i rozwój")
                      {
                        session.Kolumna = NrKolumnyKPiR.BadaniaIRozwój;
                        goto label_77;
                      }
                      break;
                    case 'p':
                      if (str == "pozostałe koszty")
                      {
                        session.Kolumna = NrKolumnyKPiR.PozostałeKoszty;
                        goto label_77;
                      }
                      break;
                  }
                  break;
                case 19:
                  if (str == "pozostałe przychody")
                  {
                    session.Kolumna = NrKolumnyKPiR.PozostałePrzychody;
                    goto label_77;
                  }
                  break;
                case 23:
                  if (str == "reprezentacja i reklama")
                  {
                    session.Kolumna = NrKolumnyKPiR.ReprezentacjaIReklama;
                    goto label_77;
                  }
                  break;
              }
            }
            if (str != null)
              throw new RowException((IRow) session, "W pliku importowym nieznana wartość KOLUMNA_KPR = '{0}'".TranslateFormat((object) pozycjaRejestruVat.KOLUMNA_KPR));
          }
label_77:
          if (!string.IsNullOrEmpty(pozycjaRejestruVat.RODZAJ_ZAKUPU))
          {
            string lower = pozycjaRejestruVat.RODZAJ_ZAKUPU.ToLower();
            if (lower != null)
            {
              switch (lower.Length)
              {
                case 4:
                  if (lower == "inne")
                  {
                    session.Rodzaj = RodzajZakupuVAT.Inne;
                    goto label_98;
                  }
                  break;
                case 6:
                  switch (lower[5])
                  {
                    case 'a':
                      if (lower == "usługa" || lower == "usluga")
                        break;
                      goto label_97;
                    case 'i':
                      if (lower == "usługi" || lower == "uslugi")
                        break;
                      goto label_97;
                    case 'o':
                      if (lower == "paliwo")
                      {
                        session.Rodzaj = RodzajZakupuVAT.Paliwo;
                        goto label_98;
                      }
                      goto label_97;
                    case 'y':
                      if (lower == "towary")
                      {
                        session.Rodzaj = RodzajZakupuVAT.Towar;
                        goto label_98;
                      }
                      goto label_97;
                    default:
                      goto label_97;
                  }
                  session.Rodzaj = RodzajZakupuVAT.Usługi;
                  goto label_98;
                case 13:
                  switch (lower[0])
                  {
                    case 'n':
                      if (lower == "nieruchomości")
                      {
                        session.Rodzaj = RodzajZakupuVAT.Nieruchomości;
                        goto label_98;
                      }
                      break;
                    case 'ś':
                      if (lower == "środki trwałe")
                      {
                        session.Rodzaj = RodzajZakupuVAT.ŚrodkiTrwałe;
                        goto label_98;
                      }
                      break;
                  }
                  break;
                case 17:
                  if (lower == "środki transportu")
                  {
                    session.Rodzaj = RodzajZakupuVAT.ŚrodkiTransportu;
                    goto label_98;
                  }
                  break;
              }
            }
label_97:
            throw new RowException((IRow) session, "W pliku importowym nieznana wartość RODZAJ_ZAKUPU = '{0}'".TranslateFormat((object) pozycjaRejestruVat.RODZAJ_ZAKUPU));
          }
label_98:
          string str1 = string.IsNullOrEmpty(pozycjaRejestruVat.RODZAJ_SPRZEDAZY) ? pozycjaRejestruVat.RODZAJ_SPRZEDAZY : pozycjaRejestruVat.RODZAJ_SPRZEDAZY.ToLower();
          if (str1 != null)
          {
            switch (str1.Length)
            {
              case 0:
                goto label_114;
              case 6:
                switch (str1[2])
                {
                  case 'l':
                    if (str1 == "uslugi" || str1 == "usluga")
                      break;
                    goto label_105;
                  case 'w':
                    if (str1 == "towary")
                    {
                      if (this.EXPORT.GetString() == "podatnikiem jest nabywca" || this.EXPORT.GetString() == "wewnątrzunijny - podatnikiem jest nabywca" || this.EXPORT.GetString() == "pozaunijny - podatnikiem jest nabywca")
                      {
                        session.RodzajSprzedazy = RodzajSprzedazyVAT.NabywcaPodatnik;
                        goto label_114;
                      }
                      session.RodzajSprzedazy = RodzajSprzedazyVAT.Towar;
                      goto label_114;
                    }
                    goto label_105;
                  case 'ł':
                    if (str1 == "usługi" || str1 == "usługa")
                      break;
                    goto label_105;
                  default:
                    goto label_105;
                }
                if (this.EXPORT.GetString() == "podatnikiem jest nabywca" || this.EXPORT.GetString() == "wewnątrzunijny - podatnikiem jest nabywca" || this.EXPORT.GetString() == "pozaunijny - podatnikiem jest nabywca")
                {
                  session.RodzajSprzedazy = RodzajSprzedazyVAT.NabywcaPodatnikUsluga;
                  goto label_114;
                }
                session.RodzajSprzedazy = RodzajSprzedazyVAT.Usługi;
                goto label_114;
              case 17:
                if (str1 == "środki transportu")
                {
                  session.RodzajSprzedazy = RodzajSprzedazyVAT.Towar;
                  goto label_114;
                }
                break;
            }
          }
label_105:
          if (str1 != null)
            throw new RowException((IRow) session, "W pliku importowym nieznana wartość RODZAJ_SPRZEDAZY = '{0}'".TranslateFormat((object) pozycjaRejestruVat.RODZAJ_SPRZEDAZY));
label_114:
          switch (string.IsNullOrEmpty(pozycjaRejestruVat.ODLICZENIA_VAT) ? pozycjaRejestruVat.ODLICZENIA_VAT : pozycjaRejestruVat.ODLICZENIA_VAT.ToLower())
          {
            case "tak":
              session.Odliczenia = OdliczeniaVAT.Tak;
              continue;
            case "nie":
              session.Odliczenia = OdliczeniaVAT.Nie;
              continue;
            case "warunkowo":
              session.Odliczenia = OdliczeniaVAT.Warunkowo;
              continue;
            case "":
            case null:
              continue;
            default:
              throw new RowException((IRow) session, "W pliku importowym nieznana wartość ODLICZENIA_VAT = '{0}'".TranslateFormat((object) pozycjaRejestruVat.ODLICZENIA_VAT));
          }
        }
      }
      handlowyEwidencja.WymagalnoscKwotyVAT = this.MPP ? WymagalnoscKwotyVAT.VATCalkowity : WymagalnoscKwotyVAT.Brak;
      Currency currency = new Currency(0M);
      List<OptimaDocument.Platnosc> platnoscList = new List<OptimaDocument.Platnosc>(this.PLATNOSCI.Cast<OptimaDocument.Platnosc>());
      platnoscList.Sort((Comparison<OptimaDocument.Platnosc>) ((p1, p2) => JestWalutowa(p2).CompareTo(JestWalutowa(p1))));
      foreach (OptimaDocument.Platnosc p in platnoscList)
      {
        Soneta.Kasa.Platnosc platnosc;
        switch (string.IsNullOrEmpty(p.KIERUNEK) ? p.KIERUNEK : p.KIERUNEK.ToLower())
        {
          case "przychód":
            platnosc = (Soneta.Kasa.Platnosc) new Naleznosc((IDokumentPlatny) handlowyEwidencja);
            break;
          case "rozchód":
            platnosc = (Soneta.Kasa.Platnosc) new Zobowiazanie((IDokumentPlatny) handlowyEwidencja);
            break;
          default:
            throw new ApplicationException("Nieznany kierunek płatności {0}".TranslateFormat((object) p.KIERUNEK));
        }
        KasaModule.GetInstance((ISessionable) this.Root.EnovaSession).Platnosci.AddRow((Row) platnosc);
        FormaPlatnosci formaPlatnosci = KasaModule.GetInstance((ISessionable) this.Root.EnovaSession).FormyPlatnosci.WgNazwy[p.FORMA_PLATNOSCI.Kod];
        if (formaPlatnosci == null)
          throw new Exception("Nie znaleziono w systemie definicji płatności o nazwie '{0}'".TranslateFormat((object) p.FORMA_PLATNOSCI.Kod));
        if (p.PODMIOT != null && !string.IsNullOrEmpty(p.PODMIOT.Typ))
          platnosc.Podmiot = (IPodmiotKasowy) p.PODMIOT.Find();
        platnosc.EwidencjaSP = formaPlatnosci.EwidencjaSP;
        platnosc.SposobZaplaty = formaPlatnosci.SposobZaplaty;
        if (JestWalutowa(p))
        {
          platnosc.Kwota = new Currency(p.KWOTA, p.WALUTA);
          TabelaKursowa tabelaKursowa = WalutyModule.GetInstance((ISessionable) this.Root.EnovaSession).TabeleKursowe.WgNazwy[p.KURS_WALUTY];
          if (tabelaKursowa != null)
            platnosc.TabelaKursowa = tabelaKursowa;
          platnosc.Kurs = p.NOTOWANIE_WALUTY_ILE * (double) p.NOTOWANIE_WALUTY_ZA_ILE;
          platnosc.KwotaKsiegi = new Currency(p.KWOTA_PLN);
          currency += platnosc.KwotaKsiegi;
        }
        else
        {
          platnosc.Kwota = new Currency(p.KWOTA);
          currency += platnosc.Kwota;
        }
        platnosc.Termin = p.TERMIN;
      }
      if (!flag)
        handlowyEwidencja.Wartosc = currency;
      if (flag)
      {
        if (handlowyEwidencja.Platnosci.Cast<Soneta.Kasa.Platnosc>().Any<Soneta.Kasa.Platnosc>((Func<Soneta.Kasa.Platnosc, bool>) (p => p.SposobZaplaty.MPP)))
          handlowyEwidencja.WymagalnoscKwotyVAT = WymagalnoscKwotyVAT.VATCalkowity;
        else
          handlowyEwidencja.WymagalnoscKwotyVAT = WymagalnoscKwotyVAT.Brak;
        this.DodajProceduryVAT(handlowyEwidencja, this.PROCEDURY_VAT);
      }
      if (this.Root.SettingSaveOA)
        this.KWOTY_DODATKOWE.Save((IZrodloOpisuAnalitycznego) null, (DokEwidencji) handlowyEwidencja);
      handlowyEwidencja.Numer.Numer = this.Root.GetOstatniNumer(handlowyEwidencja.Numer.Symbol) + 1;
      ++stats.Documents;
      return (Row) handlowyEwidencja;

      static bool JestWalutowa(OptimaDocument.Platnosc p)
      {
        return !string.IsNullOrEmpty(p.WALUTA) && p.WALUTA != "PLN";
      }
    }

    private void DodajProceduryVAT(
      HandlowyEwidencja dokument,
      OptimaDocument.RejestrVAT.ProceduryVAT proceduryVAT)
    {
      TypProceduryVAT proceduryZakupSprzedaz = ProceduryVATLogic.GetTypProceduryZakupSprzedaz(dokument.Typ);
      TypProceduryVAT typGrupyTowarowe = ProceduryVATLogic.GetTypGrupyTowarowe(dokument.Typ);
      TypProceduryVAT proceduryDokumentJpkvat = ProceduryVATLogic.GetTypProceduryDokumentJPKVAT(dokument.Typ);
      List<Soneta.Core.ProceduraVAT> procedury = dokument.Module.ProceduryVAT.WgTypu.ToList<Soneta.Core.ProceduraVAT>();
      foreach (OptimaDocument.RejestrVAT.ProceduraVAT proceduraVat in (ReadOnlyCollectionBase) proceduryVAT)
      {
        string symbol = proceduraVat.SYMBOL;
        if (symbol.StartsWith("GTU_", StringComparison.CurrentCultureIgnoreCase))
          TryAdd(symbol.Substring(4), typGrupyTowarowe);
        else if (!TryAdd(symbol, proceduryDokumentJpkvat) && !TryAdd(symbol, proceduryZakupSprzedaz))
          this.Root.importLog.WriteLine("Nie odnaleziono w bazie procedury VAT o symbolu '{0}'.".TranslateFormat((object) symbol));
      }

      bool TryAdd(string symbol, TypProceduryVAT typProceduryVAT)
      {
        Soneta.Core.ProceduraVAT proceduraVat = procedury.FirstOrDefault<Soneta.Core.ProceduraVAT>((Func<Soneta.Core.ProceduraVAT, bool>) (p => p.Symbol == symbol && p.Typ == typProceduryVAT));
        if (proceduraVat == null)
        {
          if (typProceduryVAT == TypProceduryVAT.GrupaTowarowaVAT)
            this.Root.importLog.WriteLine("Nie odnaleziono w bazie grupy towarowej o symbolu '{0}'.".TranslateFormat((object) symbol));
          return false;
        }
        if (proceduraVat.Blokada)
        {
          this.Root.importLog.WriteLine("Procedura VAT o symbolu '{0}' jest zablokowana, nie może zostać ustawiona na dokumencie.".TranslateFormat((object) symbol));
          return true;
        }
        if (typProceduryVAT == TypProceduryVAT.TypDokumentuSprzedazVAT || typProceduryVAT == TypProceduryVAT.TypDokumentuZakupVAT)
          dokument.TypDokumentuJPK = proceduraVat;
        else
          dokument.Session.AddRow<RelProceduraVAT>(new RelProceduraVAT((IProceduraVATHost) dokument)).Procedura = proceduraVat;
        return true;
      }
    }

    internal sealed class ProceduryVAT : OptimaDocument.ItemsCollection
    {
      internal const string PrivateName = "KODY_JPK";

      protected override string Name => "KODY_JPK";

      protected override string[] SubNames
      {
        get => new string[1]{ "KOD_JPK" };
      }

      internal ProceduryVAT(OptimaDocument document)
        : base(document)
      {
      }

      protected override OptimaDocument.Item CreateItem(string name)
      {
        return (OptimaDocument.Item) new OptimaDocument.RejestrVAT.ProceduraVAT(this.Root);
      }
    }

    internal class ProceduraVAT : OptimaDocument.Item
    {
      internal const string PrivateName = "KOD_JPK";
      internal string SYMBOL = string.Empty;

      internal ProceduraVAT(OptimaDocument document)
        : base(document)
      {
      }

      internal override string Name => "KOD_JPK";

      protected override bool InitializeTag(string tagName)
      {
        if (!(tagName == "KOD"))
          return false;
        this.SYMBOL = this.Root.AdvReader.ReadElementString();
        return true;
      }
    }
  }
}
