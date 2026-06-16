import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { playersApi } from '../api/players-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { AvatarChip } from '../components/ui/AvatarChip'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { useAuth } from '../state/use-auth'
import type { DuelLeaderboardEntryResponse, Guid, LeaderboardEntryResponse } from '../lib/types'

type BoardType = 'sessions' | 'duels'

type SortMode = 'winRate' | 'points' | 'games'

type BoardEntry = {
  userId: Guid
  displayName: string
  avatarColorHex: string
  winRate: number
  wins: number
  losses: number
  played: number
  points: number
  title: string | null
  subtitle: string
}

const boardOptions: ReadonlyArray<{ board: BoardType; label: string }> = [
  { board: 'sessions', label: 'Sessions' },
  { board: 'duels', label: 'Duels' },
]

const rankBadges = ['gold', 'silver', 'bronze'] as const

function sortEntries(entries: BoardEntry[], mode: SortMode): BoardEntry[] {
  const next = [...entries]

  next.sort((left, right) => {
    if (mode === 'points') {
      return right.points - left.points || right.winRate - left.winRate
    }

    if (mode === 'games') {
      return right.played - left.played || right.winRate - left.winRate
    }

    return right.winRate - left.winRate || right.points - left.points
  })

  return next
}

function fromSessions(entries: LeaderboardEntryResponse[]): BoardEntry[] {
  return entries.map((entry) => ({
    userId: entry.userId,
    displayName: entry.displayName,
    avatarColorHex: entry.avatarColorHex,
    winRate: entry.winRate,
    wins: entry.totalGamesWon,
    losses: entry.totalGamesLost,
    played: entry.totalGamesPlayed,
    points: entry.points,
    title: entry.title,
    subtitle: `${entry.totalSessions} sessions logged`,
  }))
}

function fromDuels(entries: DuelLeaderboardEntryResponse[]): BoardEntry[] {
  return entries.map((entry) => ({
    userId: entry.userId,
    displayName: entry.displayName,
    avatarColorHex: entry.avatarColorHex,
    winRate: entry.winRate,
    wins: entry.duelsWon,
    losses: entry.duelsLost,
    played: entry.duelsPlayed,
    points: entry.points,
    title: entry.title,
    subtitle: `${entry.duelsPlayed} duels played`,
  }))
}

export function LeaderboardPage() {
  const authenticatedRequest = useAuthenticatedRequest()
  const { user } = useAuth()

  const [board, setBoard] = useState<BoardType>('sessions')
  const [sessionEntries, setSessionEntries] = useState<BoardEntry[] | null>(null)
  const [duelEntries, setDuelEntries] = useState<BoardEntry[] | null>(null)
  const [sessionError, setSessionError] = useState<string | null>(null)
  const [duelError, setDuelError] = useState<string | null>(null)
  const [sortMode, setSortMode] = useState<SortMode>('winRate')

  // Each board caches independently: a board we've already fetched renders instantly and the
  // duel board is only pulled the first time it is opened. Loading/error are derived from the
  // active board's cache so the effect never has to write state synchronously.
  const entries = board === 'sessions' ? sessionEntries : duelEntries
  const error = board === 'sessions' ? sessionError : duelError
  const isLoading = entries === null && error === null

  useEffect(() => {
    if (entries !== null || error !== null) {
      return
    }

    let active = true

    const load = async () => {
      try {
        if (board === 'sessions') {
          const next = await authenticatedRequest((accessToken) => playersApi.leaderboard(accessToken))
          if (active) {
            setSessionEntries(fromSessions(next))
          }
        } else {
          const next = await authenticatedRequest((accessToken) => playersApi.duelLeaderboard(accessToken))
          if (active) {
            setDuelEntries(fromDuels(next))
          }
        }
      } catch {
        if (!active) {
          return
        }

        if (board === 'sessions') {
          setSessionError('Could not load the leaderboard.')
        } else {
          setDuelError('Could not load the leaderboard.')
        }
      }
    }

    void load()

    return () => {
      active = false
    }
  }, [board, entries, error, authenticatedRequest])

  const rankedEntries = useMemo(
    () => (entries ? sortEntries(entries, sortMode) : []),
    [entries, sortMode],
  )

  const sortOptions: ReadonlyArray<{ mode: SortMode; label: string }> = [
    { mode: 'winRate', label: 'Win Rate' },
    { mode: 'points', label: 'Points' },
    { mode: 'games', label: board === 'duels' ? 'Duels' : 'Games' },
  ]

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="High Noon Ranks"
        title="Leaderboard"
        subtitle="Crew standings by win rate, points, and games played."
        actions={
          <>
            <div className="segmented" role="group" aria-label="Choose leaderboard">
              {boardOptions.map((option) => (
                <button
                  key={option.board}
                  type="button"
                  className={
                    option.board === board
                      ? 'segmented__option segmented__option--active'
                      : 'segmented__option'
                  }
                  onClick={() => setBoard(option.board)}
                >
                  {option.label}
                </button>
              ))}
            </div>
            <div className="segmented" role="group" aria-label="Sort leaderboard">
              {sortOptions.map((option) => (
                <button
                  key={option.mode}
                  type="button"
                  className={
                    option.mode === sortMode
                      ? 'segmented__option segmented__option--active'
                      : 'segmented__option'
                  }
                  onClick={() => setSortMode(option.mode)}
                >
                  {option.label}
                </button>
              ))}
            </div>
          </>
        }
      />

      {isLoading ? <p className="state-text">Loading leaderboard...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}

      {entries && !isLoading ? (
        <Card
          title={board === 'duels' ? 'Duel Standings' : 'Crew Standings'}
          subtitle={`${rankedEntries.length} players ranked`}
        >
          {rankedEntries.length === 0 ? (
            <p className="state-text">
              {board === 'duels'
                ? 'No duels have been settled yet.'
                : 'No players have joined the crew yet.'}
            </p>
          ) : (
            <ol className="leaderboard-list">
              {rankedEntries.map((entry, index) => {
                const badge = index < rankBadges.length ? rankBadges[index] : null
                const isCurrentUser = entry.userId === user?.id

                return (
                  <motion.li
                    key={entry.userId}
                    className={
                      isCurrentUser
                        ? 'leaderboard-row leaderboard-row--self'
                        : 'leaderboard-row'
                    }
                    initial={{ opacity: 0, y: 12 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.25, ease: 'easeOut', delay: Math.min(index * 0.04, 0.4) }}
                  >
                    <span
                      className={
                        badge ? `rank-badge rank-badge--${badge}` : 'rank-badge'
                      }
                    >
                      {index + 1}
                    </span>

                    <AvatarChip
                      displayName={entry.displayName}
                      colorHex={entry.avatarColorHex}
                      size="sm"
                    />

                    <div className="leaderboard-row__identity">
                      <strong>
                        {entry.displayName}
                        {isCurrentUser ? <span className="leaderboard-row__you">You</span> : null}
                      </strong>
                      <span>{entry.title ?? entry.subtitle}</span>
                    </div>

                    <div className="leaderboard-row__stats">
                      <div className="leaderboard-stat leaderboard-stat--primary">
                        <strong>{Math.round(entry.winRate * 100)}%</strong>
                        <span>win rate</span>
                      </div>
                      <div className="leaderboard-stat">
                        <strong>{entry.wins}-{entry.losses}</strong>
                        <span>W-L</span>
                      </div>
                      <div className="leaderboard-stat">
                        <strong>{entry.points}</strong>
                        <span>points</span>
                      </div>
                    </div>
                  </motion.li>
                )
              })}
            </ol>
          )}
        </Card>
      ) : null}
    </div>
  )
}
