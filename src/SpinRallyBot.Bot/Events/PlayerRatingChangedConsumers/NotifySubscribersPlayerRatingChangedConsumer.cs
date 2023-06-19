namespace SpinRallyBot.Events.PlayerRatingChangedConsumers;

public class NotifySubscribersPlayerRatingChangedConsumer : IConsumer<PlayerRatingChanged> {
    private readonly ITelegramBotClient _bot;
    private readonly AppDbContext _db;
    private readonly IScopedMediator _mediator;

    public NotifySubscribersPlayerRatingChangedConsumer(AppDbContext db, ITelegramBotClient bot,
        IScopedMediator mediator) {
        _db = db;
        _bot = bot;
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<PlayerRatingChanged> context) {
        var subscriptions = await _db.Subscriptions
            .Where(s => s.PlayerUrl == context.Message.PlayerUrl)
            .Select(s => new { chatId = s.ChatId, playerUrl = s.PlayerUrl })
            .ToArrayAsync(context.CancellationToken);

        var exceptions = new List<Exception>();

        foreach (var subscription in subscriptions)
            try {
                await SendNotification(subscription.chatId, subscription.playerUrl, context.Message,
                    context.CancellationToken);
            } catch (Exception ex) {
                exceptions.Add(ex);
            }

        if (exceptions.Count > 0) {
            throw new AggregateException("Encountered errors while trying to update players.", exceptions);
        }
    }

    private async Task SendNotification(long chatId, string playerUrl, PlayerRatingChanged changed,
        CancellationToken cancellationToken) {
        var result = await _mediator
            .CreateRequestClient<GetPlayer>()
            .GetResponse<GetPlayerResult>(new GetPlayer(playerUrl),
                cancellationToken);

        var player = result.Message;

        var ratingDelta = player.Rating - changed.OldRating;
        var positionDelta = player.Position - changed.OldPosition;

        var text =
            $"{(ratingDelta > 0 ? "📈" : "📉")} Рейтинг обновлен ".ToEscapedMarkdownV2() + '\n' +
            $"{player.Fio}".ToEscapedMarkdownV2() + "\n" +
            $"Рейтинг: {player.Rating}({(ratingDelta > 0 ? "+" : null)}{ratingDelta:F2})"
                .ToEscapedMarkdownV2() + '\n' +
            $"Позиция: {player.Position}({(positionDelta > 0 ? "+" : null)}{positionDelta})"
                .ToEscapedMarkdownV2() + '\n' +
            $"Подписчиков: {player.Subscribers}".ToEscapedMarkdownV2() + "\n" +
            $"Обновлено: {player.Updated:dd.MM.yyyy H:mm} (МСК)".ToEscapedMarkdownV2();

        var buttons = new List<InlineKeyboardButton>();

        var findSubscriptionResponse = await _mediator
            .CreateRequestClient<FindSubscription>()
            .GetResponse<SubscriptionFound, SubscriptionNotFound>(new FindSubscription(chatId, playerUrl),
                cancellationToken);
        if (findSubscriptionResponse.Is<SubscriptionFound>(out _)) {
            buttons.Add(new InlineKeyboardButton("Отписаться") {
                CallbackData =
                    JsonSerializer.Serialize(
                        new NavigationData.ActionData(Actions.Unsubscribe, playerUrl, true))
            });
        }

        if (findSubscriptionResponse.Is<SubscriptionNotFound>(out _)) {
            buttons.Add(new InlineKeyboardButton("Подписаться") {
                CallbackData =
                    JsonSerializer.Serialize(new NavigationData.ActionData(Actions.Subscribe, playerUrl, true))
            });
        }

        buttons.Add(new InlineKeyboardButton("↩︎ Меню") {
            CallbackData = JsonSerializer.Serialize(new NavigationData.CommandData(Command.Start, newThread: true))
        });

        await _bot.SendTextMessageAsync(
            chatId,
            text,
            parseMode: ParseMode.MarkdownV2,
            replyMarkup: new InlineKeyboardMarkup(buttons.Split(1)),
            cancellationToken: cancellationToken
        );
    }
}