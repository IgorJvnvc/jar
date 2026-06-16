import { useEffect, useMemo, useRef, useState } from 'react'
import { duelsApi } from '../api/duels-api'
import { playersApi } from '../api/players-api'
import { profileApi } from '../api/profile-api'
import { AvatarChip } from '../components/ui/AvatarChip'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { formatDateTime } from '../lib/date'
import { duelStatusLabel } from '../lib/labels'
import type {
  CoinSide,
  DuelStatusResponse,
  PlayerListItemResponse,
  ProfileResponse,
} from '../lib/types'
import { useAuth } from '../state/use-auth'
import { useNotifications } from '../state/use-notifications'

type DuelHistoryFilter = 'active' | 'completed' | 'all'

const DUEL_FILTERS: ReadonlyArray<{ value: DuelHistoryFilter; label: string }> = [
  { value: 'active', label: 'Active' },
  { value: 'completed', label: 'Completed' },
  { value: 'all', label: 'All' },
]

const FALLBACK_AVATAR_COLOR = '#3a4a63'

export function DuelsPage() {
  const authenticatedRequest = useAuthenticatedRequest()
  const { user } = useAuth()
  const { latestDuelEvent } = useNotifications()

  const [profile, setProfile] = useState<ProfileResponse | null>(null)
  const [players, setPlayers] = useState<PlayerListItemResponse[]>([])
  const [duels, setDuels] = useState<DuelStatusResponse[]>([])
  const [selectedOpponentId, setSelectedOpponentId] = useState('')
  const [selectedDuelId, setSelectedDuelId] = useState<string | null>(null)
  const [debtPayment, setDebtPayment] = useState('100')
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [historyFilter, setHistoryFilter] = useState<DuelHistoryFilter>('active')

  const duelsWithRealtime = useMemo(() => {
    if (!latestDuelEvent) {
      return duels
    }

    const existing = duels.filter((item) => item.id !== latestDuelEvent.id)
    return sortDuels([latestDuelEvent, ...existing])
  }, [duels, latestDuelEvent])

  const selectedDuel = useMemo(() => {
    if (selectedDuelId) {
      return duelsWithRealtime.find((item) => item.id === selectedDuelId) ?? null
    }

    return duelsWithRealtime[0] ?? null
  }, [duelsWithRealtime, selectedDuelId])

  const visibleDuels = useMemo(() => {
    if (historyFilter === 'all') {
      return duelsWithRealtime
    }

    return duelsWithRealtime.filter((duel) =>
      historyFilter === 'completed' ? isTerminalStatus(duel.status) : !isTerminalStatus(duel.status),
    )
  }, [duelsWithRealtime, historyFilter])

  const colorById = useMemo(() => {
    const map = new Map<string, string>()
    for (const item of players) {
      map.set(item.userId, item.avatarColorHex)
    }
    if (profile) {
      map.set(profile.userId, profile.avatarColorHex)
    }
    return map
  }, [players, profile])

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const result = await authenticatedRequest(async (accessToken) => {
          const [nextProfile, nextPlayers, nextDuels] = await Promise.all([
            profileApi.getMine(accessToken),
            playersApi.list(accessToken),
            duelsApi.list(accessToken),
          ])

          return {
            nextProfile,
            nextPlayers,
            nextDuels: sortDuels(nextDuels.items),
          }
        })

        if (!active) {
          return
        }

        const filteredPlayers = result.nextPlayers.filter((item) => item.userId !== user?.id)

        setProfile(result.nextProfile)
        setPlayers(filteredPlayers)
        setDuels(result.nextDuels)
        setSelectedOpponentId((current) => current || filteredPlayers[0]?.userId || '')
        setSelectedDuelId((current) => current ?? result.nextDuels[0]?.id ?? null)
      } catch {
        if (active) {
          setError('Could not load duel data.')
        }
      } finally {
        if (active) {
          setIsLoading(false)
        }
      }
    }

    void load()

    return () => {
      active = false
    }
  }, [authenticatedRequest, user?.id])

  const refresh = async (preferDuelId?: string) => {
    const result = await authenticatedRequest(async (accessToken) => {
      const [nextProfile, nextDuels] = await Promise.all([
        profileApi.getMine(accessToken),
        duelsApi.list(accessToken),
      ])

      return {
        nextProfile,
        nextDuels: sortDuels(nextDuels.items),
      }
    })

    setProfile(result.nextProfile)
    setDuels(result.nextDuels)
    setSelectedDuelId(preferDuelId ?? result.nextDuels[0]?.id ?? null)
  }

  const handleCreateDuel = async () => {
    if (!selectedOpponentId) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const created = await authenticatedRequest((accessToken) =>
        duelsApi.create(accessToken, { opponentUserId: selectedOpponentId }),
      )
      await refresh(created.id)
      setFeedback('Challenge sent.')
    } catch {
      setError('Could not create duel. You may already have an open duel with this player.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleRespond = async (accept: boolean) => {
    if (!selectedDuel) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        duelsApi.respond(accessToken, selectedDuel.id, { accept }),
      )

      await refresh(selectedDuel.id)
      setFeedback(accept ? 'Duel accepted.' : 'Duel declined.')
    } catch {
      setError('Could not submit duel response.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleSubmitResult = async (choice: 'Won' | 'Lost') => {
    if (!selectedDuel) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        duelsApi.submitResult(accessToken, selectedDuel.id, { choice }),
      )

      await refresh(selectedDuel.id)
      setFeedback('Result submitted.')
    } catch {
      setError('Could not submit result.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleChooseSide = async (side: CoinSide) => {
    if (!selectedDuel) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        duelsApi.chooseCoinSide(accessToken, selectedDuel.id, { side }),
      )

      await refresh(selectedDuel.id)
      setFeedback(`Selected ${side.toLowerCase()}.`)
    } catch {
      setError('Could not choose coin side.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handlePayDebt = async () => {
    const parsed = Number(debtPayment)
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('Enter a valid debt payment amount.')
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const result = await authenticatedRequest((accessToken) => profileApi.payDebt(accessToken, parsed))
      setProfile(result.profile)
      setFeedback(result.paidPoints > 0 ? `Paid ${result.paidPoints} debt points.` : 'No debt payment applied.')
    } catch {
      setError('Could not pay debt.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="High Noon"
        title="Duels"
        subtitle="Challenge players, confirm results, and settle disputes with a coin flip."
      />

      {isLoading ? <p className="state-text">Loading duels...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      <Card title="Duel Economy" subtitle="Winner gets +100 points, loser gets -100 points.">
        <div className="debt-panel">
          <div>
            <p>
              Balance: <strong>{profile?.points ?? 0} pts</strong>
            </p>
            <p>
              Debt: <strong>{profile?.debtPoints ?? 0} pts</strong>
            </p>
            <p>Title: {profile?.title ?? 'No active title'}</p>
          </div>

          <div className="button-row">
            <input
              type="number"
              min={1}
              className="field-input"
              value={debtPayment}
              onChange={(event) => setDebtPayment(event.target.value)}
            />
            <button
              type="button"
              className="button button--primary"
              onClick={handlePayDebt}
              disabled={isSubmitting || (profile?.debtPoints ?? 0) <= 0 || (profile?.points ?? 0) <= 0}
            >
              Pay Debt
            </button>
          </div>
        </div>
      </Card>

      <div className="duels-layout">
        <Card title="Challenge Player" subtitle="Send a duel request to any player.">
          <label className="field-label">
            Opponent
            <select value={selectedOpponentId} onChange={(event) => setSelectedOpponentId(event.target.value)}>
              <option value="">Select opponent</option>
              {players.map((item) => (
                <option key={item.userId} value={item.userId}>
                  {item.displayName}
                </option>
              ))}
            </select>
          </label>

          <button
            type="button"
            className="button button--primary"
            onClick={handleCreateDuel}
            disabled={!selectedOpponentId || isSubmitting}
          >
            Send Challenge
          </button>

          <hr className="section-divider" />

          <div className="segmented" role="tablist" aria-label="Filter duels">
            {DUEL_FILTERS.map((option) => (
              <button
                key={option.value}
                type="button"
                role="tab"
                aria-selected={historyFilter === option.value}
                className={
                  historyFilter === option.value
                    ? 'segmented__option segmented__option--active'
                    : 'segmented__option'
                }
                onClick={() => setHistoryFilter(option.value)}
              >
                {option.label}
              </button>
            ))}
          </div>

          {duelsWithRealtime.length === 0 ? (
            <p className="state-text duel-empty">No duels yet.</p>
          ) : visibleDuels.length === 0 ? (
            <p className="state-text duel-empty">No {historyFilter} duels.</p>
          ) : (
            <ul className="stack-list">
              {visibleDuels.map((duel) => {
                const opponentName = duel.challengerId === user?.id
                  ? duel.opponentDisplayName
                  : duel.challengerDisplayName

                return (
                  <li
                    key={duel.id}
                    className={selectedDuel?.id === duel.id ? 'row-item duel-row row-item--active' : 'row-item duel-row'}
                  >
                    <button
                      type="button"
                      className="unstyled-button"
                      onClick={() => setSelectedDuelId(duel.id)}
                    >
                      <strong>vs {opponentName}</strong>
                      <span>{formatDateTime(duel.createdAtUtc)}</span>
                      <div className="duel-row__meta">
                        <StatusPill status={duel.status} />
                        <span>{duel.pointsWager} pts</span>
                      </div>
                    </button>
                  </li>
                )
              })}
            </ul>
          )}
        </Card>

        <Card title="Selected Duel" subtitle="Resolve the duel flow from this panel.">
          {selectedDuel ? (
            <div className="duel-action-grid">
              <DuelParticipants duel={selectedDuel} currentUserId={user?.id ?? ''} colorById={colorById} />
              <DuelOutcomeBanner duel={selectedDuel} currentUserId={user?.id ?? ''} />

              <div className="button-row">
                {canRespond(selectedDuel, user?.id) ? (
                  <>
                    <button
                      type="button"
                      className="button button--primary"
                      onClick={() => handleRespond(true)}
                      disabled={isSubmitting}
                    >
                      Accept
                    </button>
                    <button
                      type="button"
                      className="button button--ghost"
                      onClick={() => handleRespond(false)}
                      disabled={isSubmitting}
                    >
                      Decline
                    </button>
                  </>
                ) : null}

                {canSubmitResult(selectedDuel) ? (
                  <>
                    <button
                      type="button"
                      className="button button--primary"
                      onClick={() => handleSubmitResult('Won')}
                      disabled={isSubmitting}
                    >
                      I Won
                    </button>
                    <button
                      type="button"
                      className="button button--ghost"
                      onClick={() => handleSubmitResult('Lost')}
                      disabled={isSubmitting}
                    >
                      I Lost
                    </button>
                  </>
                ) : null}

                {canChooseCoinSide(selectedDuel) ? (
                  <>
                    <button
                      type="button"
                      className="button button--primary"
                      onClick={() => handleChooseSide('Heads')}
                      disabled={isSubmitting}
                    >
                      Heads
                    </button>
                    <button
                      type="button"
                      className="button button--ghost"
                      onClick={() => handleChooseSide('Tails')}
                      disabled={isSubmitting}
                    >
                      Tails
                    </button>
                  </>
                ) : null}
              </div>

              <CoinFlipPanel key={selectedDuel.id} duel={selectedDuel} />

              <small className="state-text">
                Round {selectedDuel.currentRound} • My choice: {selectedDuel.myLatestChoice ?? 'none'} • Opponent:
                {' '}
                {selectedDuel.opponentLatestChoice ?? 'none'}
              </small>
            </div>
          ) : (
            <p className="state-text">Select a duel to interact with it.</p>
          )}
        </Card>
      </div>
    </div>
  )
}

type DuelParticipantsProps = {
  duel: DuelStatusResponse
  currentUserId: string
  colorById: Map<string, string>
}

function DuelParticipants({ duel, currentUserId, colorById }: DuelParticipantsProps) {
  const challengerIsMe = duel.challengerId === currentUserId
  const myId = challengerIsMe ? duel.challengerId : duel.opponentId
  const theirId = challengerIsMe ? duel.opponentId : duel.challengerId
  const myName = challengerIsMe ? duel.challengerDisplayName : duel.opponentDisplayName
  const theirName = challengerIsMe ? duel.opponentDisplayName : duel.challengerDisplayName
  const myColor = colorById.get(myId) ?? FALLBACK_AVATAR_COLOR
  const theirColor = colorById.get(theirId) ?? FALLBACK_AVATAR_COLOR

  return (
    <div className="duel-players-row">
      <div>
        <AvatarChip displayName={myName} colorHex={myColor} size="sm" />
        <strong>{myName}</strong>
      </div>
      <span>vs</span>
      <div>
        <AvatarChip displayName={theirName} colorHex={theirColor} size="sm" />
        <strong>{theirName}</strong>
      </div>
      <StatusPill status={duel.status} />
    </div>
  )
}

type DuelOutcomeBannerProps = {
  duel: DuelStatusResponse
  currentUserId: string
}

function DuelOutcomeBanner({ duel, currentUserId }: DuelOutcomeBannerProps) {
  if (duel.status !== 'Completed' || !duel.winnerUserId) {
    return null
  }

  const didWin = duel.winnerUserId === currentUserId

  return (
    <div
      className={didWin ? 'duel-outcome duel-outcome--win' : 'duel-outcome duel-outcome--loss'}
      role="status"
    >
      <span className="duel-outcome__label">{didWin ? 'Victory' : 'Defeat'}</span>
      <span className="duel-outcome__points">
        {didWin ? '+' : '-'}
        {duel.pointsWager} pts
      </span>
    </div>
  )
}

type CoinFlipPanelProps = {
  duel: DuelStatusResponse
}

type CoinPhase = 'idle' | 'flipping' | 'revealed'

function CoinFlipPanel({ duel }: CoinFlipPanelProps) {
  const coinFlip = duel.coinFlip
  const resultSide = coinFlip?.resultSide ?? null

  const [phase, setPhase] = useState<CoinPhase>(resultSide ? 'revealed' : 'idle')
  const previousResultRef = useRef<CoinSide | null>(resultSide)

  useEffect(() => {
    const previous = previousResultRef.current
    previousResultRef.current = resultSide

    if (resultSide && !previous) {
      setPhase('flipping')
      const timer = window.setTimeout(() => setPhase('revealed'), 1500)
      return () => window.clearTimeout(timer)
    }

    setPhase(resultSide ? 'revealed' : 'idle')
    return undefined
  }, [resultSide])

  if (!coinFlip) {
    return null
  }

  const faceLabel = resultSide ?? '?'
  const coinClassName =
    phase === 'flipping'
      ? 'coin-anim coin-anim--flipping'
      : phase === 'revealed'
        ? 'coin-anim coin-anim--revealed'
        : 'coin-anim'

  const statusLine =
    phase === 'revealed' && resultSide
      ? `Landed on ${resultSide}.`
      : phase === 'flipping'
        ? 'Flipping...'
        : 'Waiting for both players to pick a side.'

  return (
    <div className="coin-flip-card">
      <strong>Coin Flip</strong>
      <small className="state-text">
        P1: {coinFlip.firstChooserSide ?? '-'} • P2: {coinFlip.secondChooserSide ?? '-'}
      </small>
      <div className="coin-anim-wrap">
        <div className={coinClassName} aria-live="polite">
          {faceLabel}
        </div>
      </div>
      <small className="state-text">{statusLine}</small>
    </div>
  )
}

type StatusPillProps = {
  status: DuelStatusResponse['status']
}

function StatusPill({ status }: StatusPillProps) {
  const className = getStatusClassName(status)

  return <span className={className}>{duelStatusLabel(status)}</span>
}

function canRespond(duel: DuelStatusResponse, currentUserId: string | undefined): boolean {
  if (!currentUserId) {
    return false
  }

  return duel.status === 'Pending' && duel.opponentId === currentUserId
}

function canSubmitResult(duel: DuelStatusResponse): boolean {
  if (duel.status !== 'Accepted' && duel.status !== 'AwaitingSecondReview') {
    return false
  }

  return duel.isWaitingForMyChoice
}

function canChooseCoinSide(duel: DuelStatusResponse): boolean {
  if (duel.status !== 'CoinFlipInProgress' || !duel.coinFlip) {
    return false
  }

  return duel.coinFlip.isWaitingForFirstChooser || duel.coinFlip.isWaitingForSecondChooser
}

function isTerminalStatus(status: DuelStatusResponse['status']): boolean {
  return status === 'Completed' || status === 'Declined' || status === 'Expired'
}

function sortDuels(items: DuelStatusResponse[]): DuelStatusResponse[] {
  return [...items].sort((left, right) => {
    const leftPriority = getStatusPriority(left.status)
    const rightPriority = getStatusPriority(right.status)

    if (leftPriority !== rightPriority) {
      return leftPriority - rightPriority
    }

    return new Date(right.createdAtUtc).getTime() - new Date(left.createdAtUtc).getTime()
  })
}

function getStatusPriority(status: DuelStatusResponse['status']): number {
  switch (status) {
    case 'Pending':
      return 0
    case 'Accepted':
      return 1
    case 'AwaitingSecondReview':
      return 2
    case 'CoinFlipInProgress':
      return 3
    default:
      return 10
  }
}

function getStatusClassName(status: DuelStatusResponse['status']): string {
  switch (status) {
    case 'Pending':
      return 'status-pill status-pill--pending'
    case 'Accepted':
      return 'status-pill status-pill--accepted'
    case 'AwaitingSecondReview':
      return 'status-pill status-pill--review'
    case 'CoinFlipInProgress':
      return 'status-pill status-pill--coin'
    case 'Completed':
      return 'status-pill status-pill--done'
    default:
      return 'status-pill status-pill--ended'
  }
}
