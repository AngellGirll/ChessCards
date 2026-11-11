namespace ChessCards
{
    static class EnglishRules
    {
        const string Cards = "CARDS\nPlay one card per turn, matching the color or value of the top discard. A number card gives 1–3 consecutive board actions. When no card matches, draw one. You can play it immediately if it matches. Otherwise your turn ends. The discard is reshuffled when the deck runs out, leaving the top card in place.\n\n";
        const string Reward = "\n\nREWARD\nA win earns 100 coins for the royal courtyard. Coins and repairs are saved between sessions.";
        const string Specials = "SPECIAL CARDS\nSwap exchanges sides. Undo reverses the last board action. Neither returns cards to the hand. Skip gives one action, then the opponent skips their turn.\n\n";

        public static string For(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.Checkers:
                    return "RUSSIAN CHECKERS\nTake all opposing pieces or leave the opponent with no legal move. Men move one square diagonally forward and capture in both directions. Captures are mandatory. When more than one capture is available, you may choose any.\n\n" +
                        "CAPTURE SEQUENCES\nContinue with the same piece while it can capture. A whole sequence uses one card action. Crossed-out pieces are removed when the sequence ends and cannot be jumped again during it.\n\n" +
                        "KINGS\nA man becomes a king on reaching the last rank. Kings travel along any free diagonal. Promotion during a capture takes effect immediately. A game is drawn after each side makes 15 king moves without a capture or man move.\n\n" +
                        Cards + Specials + "Swap rotates the board. Undo reverses the last completed move, including its entire capture sequence." + Reward;
                case GameMode.Go:
                    return "GO 9×9\nYou start as Black. Place stones on empty intersections. Stones connect horizontally and vertically. A group with no neighboring empty point is captured. A move cannot leave your group without liberties or repeat a position.\n\n" +
                        Cards + Specials + "PASS AND SCORING\nPass ends the turn, even before you choose a card. After two passes, tap dead groups to exclude them from the score. You can resume play if a group is disputed. Score the remaining stones and surrounded intersections. White gets 7.5 komi." + Reward;
                case GameMode.Domino:
                    return "DRAW DOMINOES\nThe double-six set has 28 tiles. You start with seven tiles and may open with any of them. Then match a tile to the left or right end of the chain.\n\n" +
                        Cards + Specials + "THE BONEYARD\nIf you cannot play a tile, draw from the boneyard until you can. Tile draws do not spend a card action. Pass if there is no legal tile and the boneyard is empty.\n\n" +
                        "WINNING\nPlay every tile to win. If the game is blocked, the lower total of pips in hand wins. Equal totals are a draw." + Reward;
                default:
                    return "CHESS\nCheckmate the bot. To move, play a card, select a piece and tap a highlighted square. The same piece may move again if the card grants more moves.\n\n" +
                        Cards + "CHECK\nCheck ends the current sequence immediately. The opponent gets one defensive move even if they draw no matching card. A check also cancels a pending skip.\n\n" +
                        Specials + "Swap rotates the board and is unavailable while either king is in check. Undo restores captured pieces and board state. You cannot undo a defense against check and then continue attacking.\n\n" +
                        "CHESS RULES\nCastling, pawn promotion and en passant are supported. An en passant capture is available only on the next chess move. Stalemate, bare kings or 50 moves without a pawn move or capture result in a draw." + Reward;
            }
        }
    }
}
