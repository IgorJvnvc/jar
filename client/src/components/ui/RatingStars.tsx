type RatingStarsProps = {
  value: number
  scale?: number
}

export function RatingStars({ value, scale = 10 }: RatingStarsProps) {
  const normalized = Math.max(0, Math.min(value, scale))
  const width = `${(normalized / scale) * 100}%`

  return (
    <div className="rating-stars" aria-label={`Rating ${normalized.toFixed(1)} of ${scale}`}>
      <span className="rating-stars__base">★★★★★★★★★★</span>
      <span className="rating-stars__filled" style={{ width }}>
        ★★★★★★★★★★
      </span>
      <strong>{normalized.toFixed(1)}</strong>
    </div>
  )
}
