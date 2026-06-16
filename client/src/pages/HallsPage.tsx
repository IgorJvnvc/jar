import { useEffect, useMemo, useState } from 'react'
import { hallsApi } from '../api/halls-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { RatingStars } from '../components/ui/RatingStars'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import type {
  AddPoolHallRequest,
  HallDayCompetitionResponse,
  PoolHallDetailResponse,
  PoolHallResponse,
  PoolHallTableResponse,
  RatePoolHallRequest,
  RatePoolHallTableRequest,
} from '../lib/types'

const defaultHallForm: AddPoolHallRequest = {
  name: '',
  address: '',
  latitude: 0,
  longitude: 0,
  totalTables: 6,
}

const defaultHallRating: RatePoolHallRequest = {
  tableQuality: 7,
  ballsQuality: 7,
  cueQuality: 7,
  priceValue: 7,
  lighting: 7,
  comment: null,
}

const defaultTableRating: RatePoolHallTableRequest = {
  clothQuality: 7,
  cushionQuality: 7,
  levelness: 7,
  comment: null,
}

export function HallsPage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [halls, setHalls] = useState<PoolHallResponse[]>([])
  const [selectedHallId, setSelectedHallId] = useState<string>('')
  const [hallDetail, setHallDetail] = useState<PoolHallDetailResponse | null>(null)
  const [competition, setCompetition] = useState<HallDayCompetitionResponse | null>(null)
  const [hallForm, setHallForm] = useState<AddPoolHallRequest>(defaultHallForm)
  const [hallRating, setHallRating] = useState<RatePoolHallRequest>(defaultHallRating)
  const [tableRating, setTableRating] = useState<RatePoolHallTableRequest>(defaultTableRating)
  const [selectedTableId, setSelectedTableId] = useState<string>('')
  const [newTableLabel, setNewTableLabel] = useState('')
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const selectedTable = useMemo(() => {
    if (!selectedTableId) {
      return null
    }

    return hallDetail?.tables.find((table) => table.id === selectedTableId) ?? null
  }, [hallDetail?.tables, selectedTableId])

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const nextHalls = await authenticatedRequest((accessToken) => hallsApi.list(accessToken))

        if (!active) {
          return
        }

        setHalls(nextHalls)
        setSelectedHallId((current) => current || nextHalls[0]?.id || '')
      } catch {
        if (active) {
          setError('Could not load hall directory.')
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
  }, [authenticatedRequest])

  useEffect(() => {
    if (!selectedHallId) {
      return
    }

    let active = true

    const loadDetail = async () => {
      try {
        const { detail, dayCompetition } = await authenticatedRequest(async (accessToken) => {
          const [detail, dayCompetition] = await Promise.all([
            hallsApi.getById(accessToken, selectedHallId),
            hallsApi.getCompetition(accessToken, selectedHallId),
          ])

          return { detail, dayCompetition }
        })

        if (!active) {
          return
        }

        setHallDetail(detail)
        setCompetition(dayCompetition)
        setSelectedTableId((current) => current || detail.tables[0]?.id || '')
      } catch {
        if (active) {
          setError('Could not load selected hall details.')
        }
      }
    }

    void loadDetail()

    return () => {
      active = false
    }
  }, [authenticatedRequest, selectedHallId])

  const refreshHalls = async (preferHallId?: string) => {
    const nextHalls = await authenticatedRequest((accessToken) => hallsApi.list(accessToken))
    setHalls(nextHalls)
    setSelectedHallId(preferHallId ?? nextHalls[0]?.id ?? '')
  }

  const handleAddHall = async () => {
    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const created = await authenticatedRequest((accessToken) => hallsApi.add(accessToken, hallForm))
      setHallForm(defaultHallForm)
      await refreshHalls(created.id)
      setFeedback('Hall added. Small point reward granted.')
    } catch {
      setError('Could not add hall.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleRateHall = async () => {
    if (!selectedHallId) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        hallsApi.rateHall(accessToken, selectedHallId, hallRating),
      )
      setFeedback('Hall rating submitted. You can rate this hall again tomorrow.')
      const detail = await authenticatedRequest((accessToken) => hallsApi.getById(accessToken, selectedHallId))
      setHallDetail(detail)
      await refreshHalls(selectedHallId)
    } catch {
      setError('Could not submit hall rating. Ensure you completed a session there today.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleRateTable = async () => {
    if (!selectedTableId) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        hallsApi.rateTable(accessToken, selectedTableId, tableRating),
      )
      setFeedback('Table rating submitted. You can still rate other tables today.')

      if (selectedHallId) {
        const detail = await authenticatedRequest((accessToken) => hallsApi.getById(accessToken, selectedHallId))
        setHallDetail(detail)
      }
    } catch {
      setError('Could not submit table rating. Ensure you completed a session on this table today.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleAddTable = async () => {
    if (!selectedHallId || !newTableLabel.trim()) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) =>
        hallsApi.addTable(accessToken, selectedHallId, { tableLabel: newTableLabel.trim() }),
      )

      const detail = await authenticatedRequest((accessToken) =>
        hallsApi.getById(accessToken, selectedHallId),
      )

      setHallDetail(detail)
      setNewTableLabel('')
      setFeedback('Table added to hall.')
    } catch {
      setError('Could not add table to this hall.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="Pool Hall Network"
        title="Halls & Tables"
        subtitle="Discover and rate halls after each session. Hall rating: once/day. Table rating: once/day per table."
      />

      {isLoading ? <p className="state-text">Loading halls...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      <div className="two-column">
        <Card title="Add New Hall" subtitle="User-driven hall map input">
          <div className="session-form-grid">
            <label className="field-label">
              Name
              <input
                value={hallForm.name}
                onChange={(event) => setHallForm((current) => ({ ...current, name: event.target.value }))}
                placeholder="BreakPoint Club"
              />
            </label>

            <label className="field-label">
              Address
              <input
                value={hallForm.address}
                onChange={(event) =>
                  setHallForm((current) => ({ ...current, address: event.target.value }))
                }
                placeholder="Main street 12"
              />
            </label>

            <label className="field-label">
              Latitude
              <input
                type="number"
                value={hallForm.latitude}
                onChange={(event) =>
                  setHallForm((current) => ({ ...current, latitude: Number(event.target.value) }))
                }
              />
            </label>

            <label className="field-label">
              Longitude
              <input
                type="number"
                value={hallForm.longitude}
                onChange={(event) =>
                  setHallForm((current) => ({ ...current, longitude: Number(event.target.value) }))
                }
              />
            </label>

            <label className="field-label">
              Table count
              <input
                type="number"
                min={1}
                value={hallForm.totalTables}
                onChange={(event) =>
                  setHallForm((current) => ({ ...current, totalTables: Number(event.target.value) }))
                }
              />
            </label>
          </div>

          <button type="button" className="button button--primary" onClick={handleAddHall} disabled={isSubmitting}>
            {isSubmitting ? 'Submitting...' : 'Add Hall'}
          </button>
        </Card>

        <Card title="Hall Directory" subtitle="Select hall to view table quality details">
          <label className="field-label">
            Hall
            <select
              value={selectedHallId}
              onChange={(event) => {
                setSelectedHallId(event.target.value)
                setSelectedTableId('')
              }}
            >
              <option value="">Select hall</option>
              {halls.map((hall) => (
                <option key={hall.id} value={hall.id}>
                  {hall.name}
                </option>
              ))}
            </select>
          </label>

          {hallDetail ? (
            <div className="hall-summary">
              <h3>{hallDetail.name}</h3>
              <p>{hallDetail.address}</p>
              <RatingStars value={hallDetail.overallScore} />
              <small>
                {hallDetail.ratingsCount} ratings • {hallDetail.totalTables} tables
              </small>

              <div className="inline-form">
                <input
                  value={newTableLabel}
                  onChange={(event) => setNewTableLabel(event.target.value)}
                  placeholder="Add table label (e.g., Table 4)"
                />
                <button
                  type="button"
                  className="button button--ghost"
                  onClick={handleAddTable}
                  disabled={!newTableLabel.trim() || isSubmitting}
                >
                  Add table
                </button>
              </div>
            </div>
          ) : (
            <p className="state-text">No hall selected.</p>
          )}
        </Card>
      </div>

      <div className="two-column">
        <Card title="Rate Selected Hall" subtitle="Available after completed session at this hall today">
          <RatingGrid
            values={hallRating}
            onChange={(next) => setHallRating(next)}
            fields={[
              ['tableQuality', 'Table quality'],
              ['ballsQuality', 'Ball quality'],
              ['cueQuality', 'Cue quality'],
              ['priceValue', 'Price value'],
              ['lighting', 'Lighting'],
            ]}
          />

          <label className="field-label">
            Comment
            <textarea
              rows={3}
              value={hallRating.comment ?? ''}
              onChange={(event) =>
                setHallRating((current) => ({
                  ...current,
                  comment: event.target.value.trim() ? event.target.value : null,
                }))
              }
            />
          </label>

          <button
            type="button"
            className="button button--primary"
            onClick={handleRateHall}
            disabled={!selectedHallId || isSubmitting}
          >
            Rate Hall
          </button>
        </Card>

        <Card title="Rate Specific Table" subtitle="One rating/day per table; rate multiple tables per day">
          <label className="field-label">
            Table
            <select value={selectedTableId} onChange={(event) => setSelectedTableId(event.target.value)}>
              <option value="">Select table</option>
              {hallDetail?.tables.map((table) => (
                <option key={table.id} value={table.id}>
                  {table.tableLabel}
                </option>
              ))}
            </select>
          </label>

          {selectedTable ? <TableSummary table={selectedTable} /> : null}

          <RatingGrid
            values={tableRating}
            onChange={(next) => setTableRating(next)}
            fields={[
              ['clothQuality', 'Cloth quality'],
              ['cushionQuality', 'Cushion quality'],
              ['levelness', 'Levelness'],
            ]}
          />

          <label className="field-label">
            Comment
            <textarea
              rows={3}
              value={tableRating.comment ?? ''}
              onChange={(event) =>
                setTableRating((current) => ({
                  ...current,
                  comment: event.target.value.trim() ? event.target.value : null,
                }))
              }
            />
          </label>

          <button
            type="button"
            className="button button--primary"
            onClick={handleRateTable}
            disabled={!selectedTableId || isSubmitting}
          >
            Rate Table
          </button>
        </Card>
      </div>

      <Card
        title="Today's Hall Competition"
        subtitle={
          competition
            ? competition.isFinalized
              ? `Finalized standings • ${competition.poolDate}`
              : `Live standings • ${competition.poolDate}`
            : 'Daily winner decided by games won'
        }
      >
        {competition ? (
          <>
            <div className="hall-summary">
              <small>
                {competition.participantCount} players • {competition.totalSessions} sessions
                {competition.winnerDisplayName ? ` • Winner: ${competition.winnerDisplayName}` : ''}
              </small>
            </div>

            {competition.entries.length === 0 ? (
              <p className="state-text">No completed sessions at this hall today.</p>
            ) : (
              <ul className="stack-list">
                {competition.entries.map((entry) => (
                  <li key={entry.userId} className="row-item">
                    <strong className="slider-value">#{entry.rank}</strong>
                    <div>
                      <strong>{entry.displayName}</strong>
                      <span>
                        {entry.gamesWon}-{entry.gamesLost} • {entry.ballsPotted} pots
                      </span>
                    </div>
                    <small>
                      {entry.sessionsCompleted} sessions • {entry.minutesPlayed} min
                    </small>
                  </li>
                ))}
              </ul>
            )}
          </>
        ) : (
          <p className="state-text">Select a hall to view today's standings.</p>
        )}
      </Card>
    </div>
  )
}

type RatingGridProps<T extends Record<string, number | string | null>> = {
  values: T
  onChange: (next: T) => void
  fields: [keyof T, string][]
}

function RatingGrid<T extends Record<string, number | string | null>>({
  values,
  onChange,
  fields,
}: RatingGridProps<T>) {
  return (
    <div className="rating-grid">
      {fields.map(([key, label]) => (
        <label key={String(key)} className="field-label">
          {label}
          <input
            type="range"
            min={1}
            max={10}
            step={1}
            value={Number(values[key] ?? 1)}
            onChange={(event) =>
              onChange({
                ...values,
                [key]: Number(event.target.value),
              })
            }
          />
          <strong className="slider-value">{values[key] as number}</strong>
        </label>
      ))}
    </div>
  )
}

type TableSummaryProps = {
  table: PoolHallTableResponse
}

function TableSummary({ table }: TableSummaryProps) {
  return (
    <div className="table-summary">
      <h3>{table.tableLabel}</h3>
      <RatingStars value={table.overallScore} />
      <small>{table.ratingsCount} ratings</small>
    </div>
  )
}
