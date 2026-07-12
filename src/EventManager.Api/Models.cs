namespace EventManager.Api;

public record EventDto(int Id, string Name, string StartDate, string Location);

public record CreateEventRequest(string Name, string StartDate, string Location);

public record MatchDto(int Id, string MatchType, string Name, int? EventId, int[] CompetitorIds);

public record CreateMatchRequest(string MatchType, string Name, int? EventId = null);

public record WeighInDto(double Weight, string Units);

public record CompetitorDto(int Id, string Name, string[] Styles, string Birthdate, WeighInDto LastWeighIn);

public record CreateCompetitorRequest(string Name, string[] Styles, string Birthdate, WeighInDto LastWeighIn);

public record BracketMatchDto(int MatchId, string MatchType, int[] CompetitorIds);

public record BracketDto(int Id, int EventId, BracketMatchDto[] Matches);

public record GenerateBracketRequest(int EventId, int[] CompetitorIds, string MatchType);
