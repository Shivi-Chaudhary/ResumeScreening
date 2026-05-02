import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const token = localStorage.getItem('rs_token');
  if (!token) return next(req);
  // Preserve existing headers (e.g. multipart boundary) — setHeaders can break FormData uploads.
  return next(
    req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`),
    }),
  );
};
