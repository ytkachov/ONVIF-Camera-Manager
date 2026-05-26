import { Link } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export function NotFoundPage() {
  return (
    <div className="p-6">
      <Card>
        <CardHeader>
          <CardTitle>404 - Page not found</CardTitle>
        </CardHeader>
        <CardContent>
          <Link to="/cameras" className="text-primary hover:underline">
            Go to cameras
          </Link>
        </CardContent>
      </Card>
    </div>
  );
}
