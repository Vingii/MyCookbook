# Seznam změn
Všechny podstatné změny projektu jsou zaznamenány v tomto souboru.

Formát vychází z [Keep a Changelog](https://keepachangelog.com/cs/1.0.0/)
a projekt dodržuje [sémantické verzování](https://semver.org/lang/cs/).

Toto je český překlad souboru `CHANGELOG.md`. Verze, které zde chybí, se zobrazí v angličtině.

## [2.6.0] - 2026-11-01
### Přidáno
- Seznam změn je nyní dostupný i v češtině.

## [2.5.0] - 2026-09-20
### Přidáno
- Recepty mohou odkazovat jeden na druhý. Napište do kroku `[[Tzatziki]]` a vznikne z toho odkaz na tento recept.
- Stránka receptu zobrazuje seznam „Použito v“ s recepty, které na ni odkazují.

## [2.4.0] - 2026-04-28
### Přidáno
- Nabídky s automatickým doplňováním pro suroviny, kategorie a štítky.
- Kliknutí prostředním tlačítkem otevře recept v nové záložce.
- Proměnlivé názvy záložek.
- Automatická mezera v množství suroviny (1kg → 1 kg).

### Změněno
- Plánovač jídel používá vyhledávání s tolerancí překlepů.
- Enter potvrzuje dialog plánovače (napsání části názvu → šipka dolů → Enter → Enter funguje správně).

## [2.3.0] - 2026-04-14
### Změněno
- Vyhledávání podle názvu receptu toleruje překlepy (nerozlišuje velikost písmen ani diakritiku).

## [2.2.0] - 2026-04-09
### Přidáno
- Recepty v plánovači jídel lze přetahovat mezi dny.

### Změněno
- Přihlášení tokenem v parametru URL je méně trvalé.
- Vylepšený vzhled dnů v plánovači jídel – barevné odlišení dnů a zvýraznění víkendů.
- Plánovač jídel na tabletech zobrazuje 2 sloupce (dříve 4).
- Oddělovače týdnů v plánovači jídel zobrazují číslo týdne.
- Názvy dnů v plánovači jídel začínají velkým písmenem.

### Opraveno
- Opraveno jméno v hlášení chyby při přihlášení tokenem.

## [2.1.0] - 2026-04-05
### Přidáno
- Podpora tokenu v parametru URL (`?token=`).

### Změněno
- Ověřování přes reverzní proxy nahrazeno OIDC.

## [2.0.10] - 2026-04-03
### Změněno
- Přechod na PostgreSQL.

## [2.0.5] - 2026-03-22
### Přidáno
- Filtrování podle surovin.
- Automatické doplňování názvů surovin.

### Změněno
- Délka kroku se ve výchozím stavu zadává v minutách.
- Prohozeno pořadí názvu a množství suroviny.
- Vylepšené chování Enteru v polích pro úpravu surovin a kroků.
- Z režimu úprav odstraněna pole pro typ a délku kroku.

### Opraveno
- Opraveno rozložení zobrazení receptu na tabletu.
- Opraveno zobrazení starých hodnot v receptu po zavření režimu úprav.

## [2.0.4] - 2026-03-12
### Přidáno
- Datum „Naposledy vařeno“ se nyní řídí (také) plánovačem jídel.

### Změněno
- Počet sloupců plánovače jídel snížen na 4.
- Nová ikona.

## [2.0.3] - 2026-03-11
### Přidáno
- Řazení v tabulce receptů.

### Opraveno
- Seznam změn nyní zobrazuje jen nové změny.

## [2.0.2] - 2026-03-11
### Změněno
- Světlejší pozadí tmavého režimu.

## [2.0.1] - 2026-03-09
### Změněno
- Světlejší pozadí tmavého režimu.

## [2.0.0] - 2026-03-09
### Přidáno
- Vytvořeno API.

### Změněno
- Nový UI framework (Vue).

## [1.5.2] - 2026-03-07
### Opraveno
- Opraveno zadávání délky kroku.

## [1.5.1] - 2026-02-08
### Opraveno
- Opraveno hlášení chyb.

## [1.5.0] - 2026-01-24
### Změněno
- Zobrazení receptu ukazuje celkovou dobu přípravy.

## [1.4.3] - 2025-11-24
### Změněno
- Zkopírované jídlo se přidá na následující den.
- Zkopírované jídlo má opačný typ než původní.

## [1.4.2] - 2025-10-31
### Změněno
- Z hlavní stránky odstraněna tlačítka pro filtrování surovin.

## [1.4.1] - 2025-10-30
### Změněno
- Interval opětovného přihlášení prodloužen na 8 hodin.

## [1.4.0] - 2025-09-30
### Přidáno
- Filtrování podle surovin v tabulkách receptů.

## [1.3.2] - 2025-09-30
### Opraveno
- Opraveno mazání receptů, na které odkazoval plánovač jídel.

## [1.3.1] - 2025-09-21
### Opraveno
- Opraven nefunkční odkaz na recept v plánovači.

## [1.3.0] - 2025-09-20
### Přidáno
- Rozbalovací nabídka v poli pro úpravu kategorie receptu.
- Podpora kliknutí prostředním tlačítkem myši v prohlížeči receptů.

### Změněno
- Adresa receptu nyní obsahuje ID receptu místo názvu.

## [1.2.1] - 2025-09-06
### Opraveno
- Opraveno neukládání suroviny při stisku Escape.

## [1.2.0] - 2025-09-06
### Opraveno
- Doplněny chybějící překlady.
- Opraveno zobrazování naplánovaných jídel všem uživatelům.
- Opraveno neukládání zvoleného jazyka mezi relacemi.

## [1.1.2] - 2025-09-04
### Opraveno
- Opraveno formátování popisu kroků.
- Vylepšen barevný kontrast na časové ose kroků.

## [1.1.1] - 2025-09-04
### Opraveno
- Opraveno nezobrazování obsahu seznamu změn.

## [1.1.0] - 2025-09-03
### Přidáno
- Tento seznam změn.
- Časová osa kroků a typy kroků (barvy).

## [1.0.0] - 2025-09-03
### Přidáno
- První veřejné vydání.
