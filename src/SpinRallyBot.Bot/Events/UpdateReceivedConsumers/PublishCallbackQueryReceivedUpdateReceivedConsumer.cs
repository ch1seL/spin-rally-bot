namespace SpinRallyBot.Events.UpdateReceivedConsumers;

public class PublishCallbackQueryReceivedUpdateReceivedConsumer : IConsumer<UpdateReceived> {
    private readonly ITelegramBotClient _botClient;
    private readonly IScopedMediator _mediator;

    public PublishCallbackQueryReceivedUpdateReceivedConsumer(IScopedMediator mediator, ITelegramBotClient botClient) {
        _mediator = mediator;
        _botClient = botClient;
    }

    public async Task Consume(ConsumeContext<UpdateReceived> context) {
        Update update = context.Message.Update;
        if (update is not {
                Type: UpdateType.CallbackQuery,
                CallbackQuery: {
                    Id: var callbackId,
                    Message: {
                        MessageId: var messageId,
                        Chat: {
                            Id: var chatId,
                            Type: var chatType
                        }
                    },
                    From.Id: var userId,
                    Data: { } data
                }
            }) {
            return;
        }

        CancellationToken cancellationToken = context.CancellationToken;

        var navigationData = JsonSerializer.Deserialize<NavigationData>(data)!;
        try {
            await _mediator.Publish(new CallbackReceived(
                navigationData,
                messageId,
                chatId,
                chatType,
                userId,
                context.Message.IsBotAdmin
            ), cancellationToken);
        } finally {
            await _botClient.AnswerCallbackQuery(callbackId, cancellationToken: cancellationToken);
        }
    }
}
