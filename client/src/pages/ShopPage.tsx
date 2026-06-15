import { useEffect, useMemo, useState } from 'react'
import type { CSSProperties } from 'react'
import { profileApi } from '../api/profile-api'
import { shopApi } from '../api/shop-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { cueRarityLabel } from '../lib/labels'
import type { CueItemResponse, ProfileResponse } from '../lib/types'

export function ShopPage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [profile, setProfile] = useState<ProfileResponse | null>(null)
  const [cues, setCues] = useState<CueItemResponse[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const result = await authenticatedRequest(async (accessToken) => {
          const [nextProfile, nextCues] = await Promise.all([
            profileApi.getMine(accessToken),
            shopApi.listCues(accessToken),
          ])

          return { nextProfile, nextCues }
        })

        if (!active) {
          return
        }

        setProfile(result.nextProfile)
        setCues(result.nextCues)
      } catch {
        if (active) {
          setError('Could not load shop catalog.')
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

  const sortedCues = useMemo(
    () =>
      [...cues].sort((left, right) => {
        if (left.isOwned !== right.isOwned) {
          return left.isOwned ? -1 : 1
        }

        if (left.shopCost === null && right.shopCost !== null) {
          return 1
        }

        if (left.shopCost !== null && right.shopCost === null) {
          return -1
        }

        return (left.shopCost ?? 0) - (right.shopCost ?? 0)
      }),
    [cues],
  )

  const reload = async () => {
    const result = await authenticatedRequest(async (accessToken) => {
      const [nextProfile, nextCues] = await Promise.all([
        profileApi.getMine(accessToken),
        shopApi.listCues(accessToken),
      ])

      return { nextProfile, nextCues }
    })

    setProfile(result.nextProfile)
    setCues(result.nextCues)
  }

  const handlePurchase = async (cue: CueItemResponse) => {
    setFeedback(null)
    setError(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) => shopApi.purchaseCue(accessToken, cue.id))
      await reload()
      setFeedback(`Purchased ${cue.name}.`)
    } catch {
      setError('Could not purchase cue.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleEquip = async (cue: CueItemResponse) => {
    setFeedback(null)
    setError(null)
    setIsSubmitting(true)

    try {
      await authenticatedRequest((accessToken) => shopApi.equipCue(accessToken, cue.id))
      await reload()
      setFeedback(`Equipped ${cue.name}.`)
    } catch {
      setError('Could not equip cue.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="Cue Workshop"
        title="Shop"
        subtitle="Buy cue skins with points or unlock special cues via achievements later."
      />

      {isLoading ? <p className="state-text">Loading shop...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      <Card title="Wallet" subtitle="Session + contribution points">
        <div className="metric-value">{profile?.points ?? 0} pts</div>
      </Card>

      <section className="cue-grid">
        {sortedCues.map((cue) => (
          <article key={cue.id} className="cue-card">
            <div className="cue-card__preview" style={{ '--cue-color': cue.colorHex } as CSSProperties}>
              <div className="cue-card__cue-shape" />
            </div>

            <div className="cue-card__content">
              <div>
                <h2>{cue.name}</h2>
                <p>{cueRarityLabel(cue.rarity)}</p>
              </div>

              <dl>
                <StatRow label="Power" value={cue.powerBonus} />
                <StatRow label="Accuracy" value={cue.accuracyBonus} />
                <StatRow label="Cue" value={cue.cueControlBonus} />
                <StatRow label="Spin" value={cue.spinBonus} />
              </dl>

              {cue.shopCost === null ? (
                <span className="cue-card__badge">Achievement only: {cue.achievementCode}</span>
              ) : (
                <span className="cue-card__badge">Cost: {cue.shopCost} pts</span>
              )}

              <div className="cue-card__actions">
                {cue.isOwned ? (
                  cue.isEquipped ? (
                    <button type="button" className="button button--ghost" disabled>
                      Equipped
                    </button>
                  ) : (
                    <button
                      type="button"
                      className="button button--primary"
                      onClick={() => handleEquip(cue)}
                      disabled={isSubmitting}
                    >
                      Equip
                    </button>
                  )
                ) : cue.shopCost === null ? (
                  <button type="button" className="button button--ghost" disabled>
                    Locked
                  </button>
                ) : (
                  <button
                    type="button"
                    className="button button--primary"
                    onClick={() => handlePurchase(cue)}
                    disabled={isSubmitting || (profile?.points ?? 0) < cue.shopCost}
                  >
                    Buy
                  </button>
                )}
              </div>
            </div>
          </article>
        ))}
      </section>
    </div>
  )
}

type StatRowProps = {
  label: string
  value: number
}

function StatRow({ label, value }: StatRowProps) {
  return (
    <div className="cue-stat-row">
      <dt>{label}</dt>
      <dd>+{value.toFixed(1)}</dd>
    </div>
  )
}
