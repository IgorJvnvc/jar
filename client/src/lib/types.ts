export type Guid = string

export type DevicePlatform = 'Android' | 'Ios'

export type UserSummary = {
  id: Guid
  displayName: string
  email: string
}

export type AuthResponse = {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAtUtc: string
  user: UserSummary
}

export type ProfileResponse = {
  userId: Guid
  displayName: string
  email: string
  avatarColorHex: string
  favoriteBallNumber: number | null
  points: number
  debtPoints: number
  title: string | null
  power: number
  accuracy: number
  cueControl: number
  spin: number
  effectivePower: number
  effectiveAccuracy: number
  effectiveCueControl: number
  effectiveSpin: number
  duelsWon: number
  duelsLost: number
  gamesWon: number
  gamesLost: number
  updatedAtUtc: string
}

export type UpdateProfileRequest = {
  displayName: string
  avatarColorHex: string
  favoriteBallNumber: number | null
}

export type CueRarity = 'Common' | 'Rare' | 'Epic' | 'Legendary'

export type CueItemResponse = {
  id: Guid
  name: string
  colorHex: string
  rarity: CueRarity
  shopCost: number | null
  achievementCode: string | null
  powerBonus: number
  accuracyBonus: number
  cueControlBonus: number
  spinBonus: number
  isOwned: boolean
  isEquipped: boolean
}

export type PoolHallResponse = {
  id: Guid
  name: string
  address: string
  latitude: number
  longitude: number
  totalTables: number
  overallScore: number
  ratingsCount: number
  createdAtUtc: string
}

export type PoolHallTableResponse = {
  id: Guid
  tableLabel: string
  overallScore: number
  ratingsCount: number
}

export type PoolHallDetailResponse = {
  id: Guid
  name: string
  address: string
  latitude: number
  longitude: number
  totalTables: number
  overallScore: number
  ratingsCount: number
  tables: PoolHallTableResponse[]
}

export type HallDayCompetitionEntryResponse = {
  userId: Guid
  displayName: string
  rank: number
  gamesWon: number
  gamesLost: number
  ballsPotted: number
  sessionsCompleted: number
  minutesPlayed: number
}

export type HallDayCompetitionResponse = {
  poolHallId: Guid
  hallName: string
  poolDate: string
  isFinalized: boolean
  winnerUserId: Guid | null
  winnerDisplayName: string | null
  participantCount: number
  totalSessions: number
  finalizedAtUtc: string | null
  entries: HallDayCompetitionEntryResponse[]
}

export type ActiveSessionPlayerResponse = {
  userId: Guid
  displayName: string
  avatarColorHex: string
  sessionId: Guid
  poolHallId: Guid
  poolHallName: string
  poolHallTableId: Guid | null
  poolHallTableLabel: string | null
  startedAtUtc: string
}

export type PlayerListItemResponse = {
  userId: Guid
  displayName: string
  email: string
  avatarColorHex: string
  points: number
  debtPoints: number
  title: string | null
}

export type LeaderboardEntryResponse = {
  userId: Guid
  displayName: string
  avatarColorHex: string
  points: number
  totalGamesPlayed: number
  totalGamesWon: number
  totalGamesLost: number
  winRate: number
  totalBallsPotted: number
  totalSessions: number
  title: string | null
}

export type DuelLeaderboardEntryResponse = {
  userId: Guid
  displayName: string
  avatarColorHex: string
  duelsWon: number
  duelsLost: number
  duelsPlayed: number
  winRate: number
  points: number
  title: string | null
}

export type SessionEndReason = 'Manual' | 'AutoIdle' | 'AutoDayClose'

export type SessionResponse = {
  id: Guid
  poolHallId: Guid
  poolHallTableId: Guid | null
  startedAtUtc: string
  endedAtUtc: string | null
  isActive: boolean
  isFlaggedForValidation: boolean
  ballsPotted: number
  ballsPottedOnBreak: number
  gamesWon: number
  gamesLost: number
  gamesBroken: number
  snookersFaced: number
  snookersEscaped: number
  goldenBreaks: number
  powerDelta: number
  accuracyDelta: number
  cueControlDelta: number
  spinDelta: number
  awardedPoints: number
  notes: string | null
  endReason: SessionEndReason | null
}

export type StartSessionRequest = {
  poolHallId: Guid
  poolHallTableId: Guid | null
}

export type GameType = 'EightBall' | 'NineBall'

export type GameLogEntry = {
  gameType: GameType
  brokeThisRack: boolean
  breakPots: number
  ballsPotted: number
  snookersFaced: number
  snookersEscaped: number
  won: boolean
  goldenBreak: boolean
}

export type EndSessionRequest = {
  games: GameLogEntry[]
  notes: string | null
}

export type AddPoolHallRequest = {
  name: string
  address: string
  latitude: number
  longitude: number
  totalTables: number
}

export type AddPoolHallTableRequest = {
  tableLabel: string
}

export type RatePoolHallRequest = {
  tableQuality: number
  ballsQuality: number
  cueQuality: number
  priceValue: number
  lighting: number
  comment: string | null
}

export type RatePoolHallTableRequest = {
  clothQuality: number
  cushionQuality: number
  levelness: number
  comment: string | null
}

export type DuelStatus =
  | 'Pending'
  | 'Declined'
  | 'Accepted'
  | 'AwaitingSecondReview'
  | 'CoinFlipInProgress'
  | 'Completed'
  | 'Expired'

export type DuelResultChoice = 'Won' | 'Lost'

export type CoinSide = 'Heads' | 'Tails'

export type DuelCoinFlipResponse = {
  duelId: Guid
  firstChooserUserId: Guid
  secondChooserUserId: Guid | null
  firstChooserSide: CoinSide | null
  secondChooserSide: CoinSide | null
  resultSide: CoinSide | null
  winnerUserId: Guid | null
  isWaitingForFirstChooser: boolean
  isWaitingForSecondChooser: boolean
  isResolved: boolean
}

export type DuelStatusResponse = {
  id: Guid
  challengerId: Guid
  challengerDisplayName: string
  opponentId: Guid
  opponentDisplayName: string
  status: DuelStatus
  pointsWager: number
  currentRound: number
  myLatestChoice: DuelResultChoice | null
  opponentLatestChoice: DuelResultChoice | null
  isWaitingForMyChoice: boolean
  coinFlip: DuelCoinFlipResponse | null
  winnerUserId: Guid | null
  createdAtUtc: string
  completedAtUtc: string | null
}

export type DuelListResponse = {
  items: DuelStatusResponse[]
}
