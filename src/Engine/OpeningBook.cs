using System;
using System.Collections.Generic;

namespace Sokol.Engine
{
    /// <summary>
    /// Small built-in opening book with Russian opening names.
    /// Format of each entry: "Name|defining moves|continuation". The name applies to the position
    /// after the defining moves; defining + continuation together form a book line.
    /// </summary>
    public static class OpeningBook
    {
        static readonly string[] Data =
        {
            "Королевский пешечный дебют|e4|",
            "Ферзевый пешечный дебют|d4|",
            "Английское начало|c4|",
            "Дебют Рети|Nf3|",
            "Дебют Берда|f4|Nf6 Nf3 g6 e3 Bg7 b3 d6 Bb2 O-O Be2",
            "Дебют Ларсена|b3|e5 Bb2 Nc6 e3 d5 Bb5 Bd6",
            "Дебют Сокольского|b4|e5 Bb2 Bxb4 Bxe5 Nf6 Nf3 O-O",

            "Открытая игра|e4 e5|",
            "Дебют королевского коня|e4 e5 Nf3|",
            "Дебют королевского коня|e4 e5 Nf3 Nc6|",
            "Итальянская партия|e4 e5 Nf3 Nc6 Bc4|",
            "Итальянская партия: Джуоко пиано|e4 e5 Nf3 Nc6 Bc4 Bc5|c3 Nf6 d3 d6 O-O O-O Re1 a6 a4",
            "Итальянская партия: Джуоко пьяниссимо|e4 e5 Nf3 Nc6 Bc4 Bc5 d3|Nf6 c3 d6 O-O O-O Re1 a6",
            "Итальянская партия: Главный вариант|e4 e5 Nf3 Nc6 Bc4 Bc5 c3 Nf6 d4|exd4 cxd4 Bb4+ Bd2 Bxd2+ Nbxd2 d5 exd5 Nxd5",
            "Гамбит Эванса|e4 e5 Nf3 Nc6 Bc4 Bc5 b4|Bxb4 c3 Ba5 d4 exd4 O-O",
            "Защита двух коней|e4 e5 Nf3 Nc6 Bc4 Nf6|d3 Be7 O-O O-O Re1 d6 c3",
            "Защита двух коней: Атака конём на g5|e4 e5 Nf3 Nc6 Bc4 Nf6 Ng5|d5 exd5 Na5 Bb5+ c6 dxc6 bxc6 Be2 h6 Nf3 e4 Ne5",
            "Венгерская защита|e4 e5 Nf3 Nc6 Bc4 Be7|d4 d6",
            "Испанская партия|e4 e5 Nf3 Nc6 Bb5|",
            "Испанская партия: Закрытый вариант|e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7|Re1 b5 Bb3 d6 c3 O-O h3 Na5 Bc2 c5 d4 Qc7",
            "Испанская партия: Анти-Маршалл|e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7 Re1 b5 Bb3 O-O a4|b4",
            "Испанская партия: Атака Маршалла|e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7 Re1 b5 Bb3 O-O c3 d5|exd5 Nxd5 Nxe5 Nxe5 Rxe5 c6",
            "Испанская партия: Берлинская защита|e4 e5 Nf3 Nc6 Bb5 Nf6|O-O Nxe4 d4 Nd6 Bxc6 dxc6 dxe5 Nf5 Qxd8+ Kxd8",
            "Испанская партия: Берлинская защита|e4 e5 Nf3 Nc6 Bb5 Nf6 d3|Bc5 c3 O-O O-O d6",
            "Испанская партия: Разменный вариант|e4 e5 Nf3 Nc6 Bb5 a6 Bxc6|dxc6 O-O f6 d4",
            "Испанская партия: Открытый вариант|e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Nxe4|d4 b5 Bb3 d5 dxe5 Be6",
            "Шотландская партия|e4 e5 Nf3 Nc6 d4|exd4 Nxd4 Nf6 Nxc6 bxc6 e5 Qe7 Qe2 Nd5 c4",
            "Шотландская партия|e4 e5 Nf3 Nc6 d4 exd4 Nxd4 Bc5|Be3 Qf6 c3 Nge7 Bc4",
            "Шотландский гамбит|e4 e5 Nf3 Nc6 d4 exd4 Bc4|Bc5 c3 Nf6 cxd4 Bb4+ Bd2",
            "Русская партия (защита Петрова)|e4 e5 Nf3 Nf6|Nxe5 d6 Nf3 Nxe4 d4 d5 Bd3 Nc6 O-O Be7",
            "Русская партия (защита Петрова)|e4 e5 Nf3 Nf6 d4|Nxe4 Bd3 d5 Nxe5 Nd7 Nxd7 Bxd7 O-O",
            "Защита Филидора|e4 e5 Nf3 d6|d4 Nf6 Nc3 Nbd7 Bc4 Be7 O-O O-O",
            "Королевский гамбит|e4 e5 f4|exf4 Nf3 g5 h4 g4 Ne5",
            "Королевский гамбит|e4 e5 f4 exf4 Nf3 d6|d4 g5",
            "Отказанный королевский гамбит|e4 e5 f4 Bc5|Nf3 d6 c3",
            "Венская партия|e4 e5 Nc3|Nf6 f4 d5 fxe5 Nxe4 Nf3 Be7",
            "Венская партия|e4 e5 Nc3 Nc6|Bc4 Bc5 Qg4",
            "Партия четырёх коней|e4 e5 Nf3 Nc6 Nc3 Nf6|Bb5 Bb4 O-O O-O d3 d6",
            "Партия четырёх коней: Шотландский вариант|e4 e5 Nf3 Nc6 Nc3 Nf6 d4|exd4 Nxd4 Bb4 Nxc6 bxc6 Bd3 d5 exd5 cxd5 O-O O-O",
            "Дебют Понциани|e4 e5 Nf3 Nc6 c3|Nf6 d4 Nxe4 d5 Ne7 Nxe5 Ng6",
            "Центральный дебют|e4 e5 d4 exd4|Qxd4 Nc6 Qe3 Nf6 Nc3 Bb4 Bd2 O-O O-O-O Re8",
            "Дебют слона|e4 e5 Bc4|Nf6 d3 c6 Nf3 d5 Bb3 Bd6",

            "Сицилианская защита|e4 c5|",
            "Сицилианская защита: Открытый вариант|e4 c5 Nf3 d6 d4 cxd4 Nxd4|",
            "Сицилианская защита: Вариант Найдорфа|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6|Be3 e5 Nb3 Be6 f3 Be7 Qd2 O-O O-O-O",
            "Сицилианская защита: Вариант Найдорфа|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6 Bg5|e6 f4 Be7 Qf3 Qc7 O-O-O Nbd7",
            "Сицилианская защита: Вариант Найдорфа|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6 Be2|e5 Nb3 Be7 O-O O-O",
            "Сицилианская защита: Вариант дракона|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 g6|Be3 Bg7 f3 O-O Qd2 Nc6 Bc4 Bd7 O-O-O",
            "Сицилианская защита: Классический вариант|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 Nc6|",
            "Сицилианская защита: Атака Рихтера — Раузера|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 Nc6 Bg5|e6 Qd2 Be7 O-O-O O-O",
            "Сицилианская защита: Схевенингенский вариант|e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 e6|Be2 Be7 O-O O-O f4 Nc6",
            "Сицилианская защита: Вариант Свешникова|e4 c5 Nf3 Nc6 d4 cxd4 Nxd4 Nf6 Nc3 e5|Ndb5 d6 Bg5 a6 Na3 b5 Bxf6 gxf6 Nd5 f5",
            "Сицилианская защита: Ускоренный дракон|e4 c5 Nf3 Nc6 d4 cxd4 Nxd4 g6|Nc3 Bg7 Be3 Nf6 Bc4 O-O Bb3 d6",
            "Сицилианская защита: Вариант Тайманова|e4 c5 Nf3 e6 d4 cxd4 Nxd4 Nc6|Nc3 Qc7 Be2 a6 O-O Nf6 Be3 Bb4",
            "Сицилианская защита: Вариант Паульсена|e4 c5 Nf3 e6 d4 cxd4 Nxd4 a6|Bd3 Nf6 O-O Qc7 Qe2 d6 c4",
            "Сицилианская защита: Вариант четырёх коней|e4 c5 Nf3 e6 d4 cxd4 Nxd4 Nf6 Nc3 Nc6|Ndb5 Bb4 a3 Bxc3+ Nxc3 d5",
            "Сицилианская защита: Вариант Чеховера|e4 c5 Nf3 d6 d4 cxd4 Qxd4|Nc6 Bb5 Bd7 Bxc6 Bxc6 Nc3 Nf6 Bg5 e6 O-O-O",
            "Сицилианская защита: Вариант Алапина|e4 c5 c3|Nf6 e5 Nd5 d4 cxd4 Nf3 Nc6 cxd4 d6 Bc4",
            "Сицилианская защита: Вариант Алапина|e4 c5 c3 d5|exd5 Qxd5 d4 Nf6 Nf3 e6 Be2 Nc6 O-O",
            "Сицилианская защита: Закрытый вариант|e4 c5 Nc3|Nc6 g3 g6 Bg2 Bg7 d3 d6 f4 e6 Nf3 Nge7 O-O O-O",
            "Сицилианская защита: Атака Гран-при|e4 c5 Nc3 Nc6 f4|g6 Nf3 Bg7 Bb5 Nd4",
            "Сицилианская защита: Вариант Россолимо|e4 c5 Nf3 Nc6 Bb5|g6 O-O Bg7 Re1 e5 b4",
            "Сицилианская защита: Московский вариант|e4 c5 Nf3 d6 Bb5+|Bd7 Bxd7+ Qxd7 c4 Nc6 Nc3 Nf6 d4",
            "Сицилианская защита: Гамбит Морра|e4 c5 d4 cxd4 c3|dxc3 Nxc3 Nc6 Nf3 d6 Bc4 e6 O-O Nf6 Qe2 Be7 Rd1 e5",

            "Французская защита|e4 e6|d4 d5",
            "Французская защита: Вариант Винавера|e4 e6 d4 d5 Nc3 Bb4|e5 c5 a3 Bxc3+ bxc3 Ne7 Qg4 Qc7",
            "Французская защита: Классический вариант|e4 e6 d4 d5 Nc3 Nf6|Bg5 Be7 e5 Nfd7 Bxe7 Qxe7 f4 O-O Nf3 c5",
            "Французская защита: Вариант Стейница|e4 e6 d4 d5 Nc3 Nf6 e5|Nfd7 f4 c5 Nf3 Nc6 Be3",
            "Французская защита: Вариант продвижения|e4 e6 d4 d5 e5|c5 c3 Nc6 Nf3 Qb6 a3 c4",
            "Французская защита: Вариант Тарраша|e4 e6 d4 d5 Nd2|c5 exd5 exd5 Ngf3 Nc6 Bb5 Bd6 O-O",
            "Французская защита: Вариант Тарраша|e4 e6 d4 d5 Nd2 Nf6|e5 Nfd7 Bd3 c5 c3 Nc6 Ne2",
            "Французская защита: Разменный вариант|e4 e6 d4 d5 exd5|exd5 Nf3 Nf6 Bd3 Bd6 O-O O-O",
            "Французская защита: Вариант Рубинштейна|e4 e6 d4 d5 Nc3 dxe4|Nxe4 Nd7 Nf3 Ngf6 Nxf6+ Nxf6",
            "Французская защита: Староиндийская атака|e4 e6 d3|d5 Nd2 Nf6 Ngf3 c5 g3 Nc6 Bg2 Be7 O-O O-O Re1",

            "Защита Каро-Канн|e4 c6|d4 d5",
            "Защита Каро-Канн: Классический вариант|e4 c6 d4 d5 Nc3 dxe4 Nxe4 Bf5|Ng3 Bg6 h4 h6 Nf3 Nd7 h5 Bh7 Bd3 Bxd3 Qxd3",
            "Защита Каро-Канн: Вариант Карпова|e4 c6 d4 d5 Nc3 dxe4 Nxe4 Nd7|Nf3 Ngf6 Nxf6+ Nxf6 c3",
            "Защита Каро-Канн: Вариант продвижения|e4 c6 d4 d5 e5|Bf5 Nf3 e6 Be2 c5 Be3",
            "Защита Каро-Канн: Разменный вариант|e4 c6 d4 d5 exd5 cxd5|Bd3 Nc6 c3 Nf6 Bf4 Bg4 Qb3",
            "Защита Каро-Канн: Атака Панова|e4 c6 d4 d5 exd5 cxd5 c4|Nf6 Nc3 e6 Nf3 Be7 cxd5 Nxd5",
            "Защита Каро-Канн: Вариант двух коней|e4 c6 Nc3 d5 Nf3|Bg4 h3 Bxf3 Qxf3 e6 d4 Nf6",
            "Защита Каро-Канн: Фантазийный вариант|e4 c6 d4 d5 f3|dxe4 fxe4 e5 Nf3 exd4 Bc4",

            "Защита Пирца|e4 d6|d4 Nf6 Nc3 g6 Be3 Bg7 Qd2 c6 f3 b5",
            "Современная защита|e4 g6|d4 Bg7 Nc3 d6 Be3 a6 Qd2 Nd7",
            "Защита Алехина|e4 Nf6|e5 Nd5 d4 d6 Nf3 Bg4 Be2 e6 O-O Be7 c4 Nb6",
            "Скандинавская защита|e4 d5|exd5 Qxd5 Nc3 Qa5 d4 Nf6 Nf3 c6 Bc4 Bf5 Bd2 e6",
            "Скандинавская защита|e4 d5 exd5 Qxd5 Nc3 Qd6|d4 Nf6 Nf3 a6 g3",
            "Скандинавская защита|e4 d5 exd5 Nf6|d4 Nxd5 c4 Nb6 Nf3 g6",
            "Защита Нимцовича|e4 Nc6|d4 d5 e5 Bf5 Nf3 e6",

            "Закрытая игра|d4 d5|",
            "Ферзевый гамбит|d4 d5 c4|",
            "Отказанный ферзевый гамбит|d4 d5 c4 e6|Nc3 Nf6 Bg5 Be7 e3 O-O Nf3 h6 Bh4 b6",
            "Отказанный ферзевый гамбит: Разменный вариант|d4 d5 c4 e6 Nc3 Nf6 cxd5|exd5 Bg5 Be7 e3 c6 Bd3 Nbd7 Qc2 O-O Nge2",
            "Отказанный ферзевый гамбит: Защита Рагозина|d4 d5 c4 e6 Nc3 Nf6 Nf3 Bb4|cxd5 exd5 Bg5 Nbd7",
            "Защита Тарраша|d4 d5 c4 e6 Nc3 c5|cxd5 exd5 Nf3 Nc6 g3 Nf6 Bg2 Be7 O-O O-O",
            "Принятый ферзевый гамбит|d4 d5 c4 dxc4|Nf3 Nf6 e3 e6 Bxc4 c5 O-O a6",
            "Славянская защита|d4 d5 c4 c6|Nf3 Nf6 Nc3 dxc4 a4 Bf5 e3 e6 Bxc4 Bb4 O-O",
            "Славянская защита: Разменный вариант|d4 d5 c4 c6 cxd5|cxd5 Nc3 Nf6 Bf4 Nc6 e3 Bf5",
            "Полуславянская защита|d4 d5 c4 c6 Nc3 Nf6 Nf3 e6|e3 Nbd7 Bd3 dxc4 Bxc4 b5 Bd3",
            "Контргамбит Альбина|d4 d5 c4 e5|dxe5 d4 Nf3 Nc6 g3",
            "Защита Чигорина|d4 d5 c4 Nc6|Nc3 dxc4 Nf3 Nf6 e4 Bg4 Be3",
            "Лондонская система|d4 d5 Bf4|Nf6 e3 e6 Nf3 c5 c3 Nc6 Nbd2 Bd6 Bg3 O-O Bd3",
            "Лондонская система|d4 Nf6 Bf4|g6 e3 Bg7 Nf3 O-O Be2 d6 h3",
            "Лондонская система: Вариант Жобавы|d4 d5 Nc3 Nf6 Bf4|e6 e3 Bd6 Nb5",
            "Система Колле — Цукерторта|d4 d5 Nf3 Nf6 e3|e6 Bd3 c5 b3 Nc6 O-O Bd6 Bb2 O-O",
            "Атака Торре|d4 Nf6 Nf3 e6 Bg5|c5 e3 h6 Bh4 b6",
            "Атака Тромповского|d4 Nf6 Bg5|Ne4 Bf4 c5 f3 Qa5+ c3 Nf6 d5",

            "Индийская защита|d4 Nf6|c4",
            "Индийская защита|d4 Nf6 c4 e6|",
            "Староиндийская защита|d4 Nf6 c4 g6 Nc3 Bg7|e4 d6 Nf3 O-O Be2 e5 O-O Nc6 d5 Ne7",
            "Староиндийская защита: Вариант Земиша|d4 Nf6 c4 g6 Nc3 Bg7 e4 d6 f3|O-O Be3 e5 d5",
            "Староиндийская защита: Вариант фианкетто|d4 Nf6 c4 g6 Nf3 Bg7 g3|O-O Bg2 d6 O-O Nc6 Nc3 a6",
            "Защита Грюнфельда|d4 Nf6 c4 g6 Nc3 d5|cxd5 Nxd5 e4 Nxc3 bxc3 Bg7 Nf3 c5 Be3 Qa5 Qd2",
            "Нимцо-индийская защита|d4 Nf6 c4 e6 Nc3 Bb4|e3 O-O Bd3 d5 Nf3 c5 O-O",
            "Нимцо-индийская защита: Вариант Капабланки|d4 Nf6 c4 e6 Nc3 Bb4 Qc2|O-O a3 Bxc3+ Qxc3 b6 Bg5",
            "Новоиндийская защита|d4 Nf6 c4 e6 Nf3 b6|g3 Ba6 b3 Bb4+ Bd2 Be7 Bg2 c6 Bc3 d5",
            "Защита Боголюбова|d4 Nf6 c4 e6 Nf3 Bb4+|Bd2 Qe7 g3 Nc6 Nc3 Bxc3 Bxc3 Ne4",
            "Каталонское начало|d4 Nf6 c4 e6 g3|d5 Bg2 Be7 Nf3 O-O O-O dxc4 Qc2 a6 Qxc4 b5 Qc2 Bb7",
            "Защита Бенони|d4 Nf6 c4 c5 d5|e6 Nc3 exd5 cxd5 d6 e4 g6 Nf3 Bg7 Be2 O-O O-O",
            "Волжский гамбит|d4 Nf6 c4 c5 d5 b5|cxb5 a6 bxa6 Bxa6 Nc3 d6 e4 Bxf1 Kxf1 g6",
            "Голландская защита|d4 f5|g3 Nf6 Bg2 e6 Nf3 Be7 O-O O-O c4 d6 Nc3",
            "Голландская защита: Ленинградский вариант|d4 f5 c4 Nf6 g3 g6|Bg2 Bg7 Nf3 O-O O-O d6 Nc3",
            "Голландская защита: Каменная стена|d4 f5 c4 Nf6 g3 e6 Bg2 d5|Nf3 c6 O-O Bd6 b3 Qe7",

            "Английское начало: Обращённая сицилианская|c4 e5|Nc3 Nf6 Nf3 Nc6 g3 d5 cxd5 Nxd5 Bg2 Nb6 O-O Be7",
            "Английское начало: Вариант четырёх коней|c4 e5 Nc3 Nf6 Nf3 Nc6|e3 Bb4 Qc2 O-O",
            "Английское начало: Симметричный вариант|c4 c5|Nc3 Nc6 g3 g6 Bg2 Bg7 Nf3 Nf6 O-O O-O",
            "Английское начало: Симметричный вариант|c4 c5 Nf3 Nf6 Nc3 Nc6 g3 g6 Bg2 Bg7 O-O O-O d4|cxd4 Nxd4",
            "Английское начало: Вариант Микенаса|c4 Nf6 Nc3 e6 e4|d5 e5 d4",
            "Дебют Рети|Nf3 d5 c4|e6 g3 Nf6 Bg2 Be7 O-O O-O b3 c5",
            "Староиндийское начало|Nf3 d5 g3|Nf6 Bg2 c6 O-O Bg4 d3 Nbd7 Nbd2 e5 e4",
        };

        sealed class Entry
        {
            public readonly Dictionary<int, int> Moves = new Dictionary<int, int>();
            public int Ply;
        }

        static Dictionary<ulong, Entry> positions;
        static Dictionary<ulong, string> names;
        static readonly object sync = new object();
        public static readonly List<string> Errors = new List<string>();

        static void EnsureLoaded()
        {
            if (positions != null) return;
            lock (sync)
            {
                if (positions != null) return;
                var pos = new Dictionary<ulong, Entry>();
                var nm = new Dictionary<ulong, string>();
                foreach (string line in Data)
                {
                    string[] parts = line.Split('|');
                    var b = new Board();
                    var tokens = new List<string>();
                    tokens.AddRange(parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    int definingCount = tokens.Count;
                    if (parts.Length > 2) tokens.AddRange(parts[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        int m = Notation.ParseSan(b, tokens[i]);
                        if (m == Move.None)
                        {
                            Errors.Add($"{parts[0]}: illegal move '{tokens[i]}' at ply {i + 1}");
                            break;
                        }
                        if (!pos.TryGetValue(b.Hash, out Entry e))
                        {
                            e = new Entry { Ply = i };
                            pos[b.Hash] = e;
                        }
                        e.Moves.TryGetValue(m, out int w);
                        e.Moves[m] = w + 1;
                        b.MakeMove(m);
                        if (i == definingCount - 1 && parts[0].Length > 0) nm[b.Hash] = parts[0];
                    }
                }
                names = nm;
                positions = pos;
            }
        }

        /// <summary>True if move m is a book move in position b.</summary>
        public static bool IsBookMove(Board b, int m)
        {
            EnsureLoaded();
            return positions.TryGetValue(b.Hash, out Entry e) && e.Moves.ContainsKey(m);
        }

        public static bool InBook(Board b)
        {
            EnsureLoaded();
            return positions.ContainsKey(b.Hash);
        }

        /// <summary>Opening name of exactly this position, or null.</summary>
        public static string NameOf(Board b)
        {
            EnsureLoaded();
            return names.TryGetValue(b.Hash, out string n) ? n : null;
        }

        /// <summary>Picks a weighted random book move, or Move.None.</summary>
        public static int Pick(Board b, Random rng)
        {
            EnsureLoaded();
            if (!positions.TryGetValue(b.Hash, out Entry e) || e.Moves.Count == 0) return Move.None;
            double total = 0;
            foreach (var kv in e.Moves) total += Math.Sqrt(kv.Value);
            double r = rng.NextDouble() * total;
            foreach (var kv in e.Moves)
            {
                r -= Math.Sqrt(kv.Value);
                if (r <= 0) return b.IsLegal(kv.Key) ? kv.Key : Move.None;
            }
            return Move.None;
        }

        public static int LoadedPositions { get { EnsureLoaded(); return positions.Count; } }
    }
}
